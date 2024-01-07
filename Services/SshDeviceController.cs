using System.Runtime.CompilerServices;
using Renci.SshNet;
using System.Collections.Concurrent;

namespace TuringPi.Panel.Services;

public abstract class SshDeviceController : IDisposable
{
  private readonly ConcurrentDictionary<string, SshClient> ConnectionPool = new();
  private bool disposedValue;

  private ILogger Logger { get; }

  public SshDeviceController(ILogger logger)
  {
    this.Logger = logger;
  }

  /// <summary>
  /// Establish an SSH connection to the specified nodes and cache the connections for later use.
  /// </summary>
  /// <param name="nodes"></param>
  /// <remarks>
  /// SSH connections take a while, this function is used to establish connections while the startup 
  /// progress display is running.
  /// </remarks>
  public async Task Connect(IEnumerable<AppSettings.Node> nodes)
  {
    await Parallel.ForEachAsync(nodes, async (node, cancellationToken) => { await GetConnection(node); });
  }

  /// <summary>
  /// Create a new SSH client using settings from config.
  /// </summary>
  /// <param name="node"></param>
  /// <returns></returns>
  private SshClient CreateNewSshClient(AppSettings.Node node)
  {
    if (!String.IsNullOrEmpty(node.KeyFile))
    {
      if (!String.IsNullOrEmpty(node.KeyFilePassPhrase))
      {
        this.Logger.LogTrace("Using keyfile {keyfile} and passphrase for {node}", node.KeyFile, node.Name);
        return new(node.HostName, node.UserName, new PrivateKeyFile(node.KeyFile, node.KeyFilePassPhrase));
      }
      else
      {
        this.Logger.LogTrace("Using keyfile {keyfile}/no passphrase for {node}", node.KeyFile, node.Name);
        return new(node.HostName, node.UserName, new PrivateKeyFile(node.KeyFile));
      }
    }
    else
    {
      this.Logger.LogTrace("Using password for {node}", node.Name);
      return new(node.HostName, node.UserName, node.Password);
    }
  }

  /// <summary>
  /// Return a cached SSH connection, or create a new one and add to the cache.  If no connection could be made, return null.
  /// </summary>
  /// <param name="node"></param>
  /// <returns></returns>
  protected async Task<SshClient?> GetConnection(AppSettings.Node node)
  {
    CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(20));

    if (ConnectionPool.TryGetValue(node.HostName, out var pooledConnection))
    {
      if (!pooledConnection.IsConnected)
      {
        try
        {
          await pooledConnection.ConnectAsync(cancellationTokenSource.Token);
        }
        catch (System.Net.Sockets.SocketException ex)
        {
          this.Logger.LogInformation("{node}/[{host}] is offline or unreachable ({errorCode:X}).", node.Name, node.HostName, ex.ErrorCode);
        }
        catch (Exception ex)
        {
          this.Logger.LogError(ex, "GetConnection {node}", node.Name);
        }
      }

      if (pooledConnection.IsConnected)
      {
        return pooledConnection;
      }
    }

    try
    {
      SshClient ssh = CreateNewSshClient(node);

      ssh.HostKeyReceived += (sender, e) =>
      {
        e.CanTrust = node.ExpectedFingerPrint == null || node.ExpectedFingerPrint.Equals(e.FingerPrintSHA256);
      };

      await ssh.ConnectAsync(cancellationTokenSource.Token);

      if (ssh.IsConnected)
      {
        ConnectionPool.TryAdd(node.HostName, ssh);
        return ssh;
      }
    }
    catch (System.Net.Sockets.SocketException ex)
    {
      this.Logger.LogInformation("{node}/[{host}] is offline or unreachable ({errorCode:X}).", node.Name, node.HostName, ex.ErrorCode);
    }
    catch (System.IO.FileNotFoundException ex)
    {
      this.Logger.LogInformation("{node}/[{host}]: {ex}.", node.Name, node.HostName, ex.Message);
    }
    catch (Exception ex)
    {
      this.Logger.LogError(ex, "GetConnection {node}", node.Name);
    }

    return null;
  }

  /// <summary>
  /// Execute the specified query using SSH and return the response.
  /// </summary>
  /// <param name="node"></param>
  /// <param name="command"></param>
  /// <param name="caller"></param>
  /// <returns></returns>
  /// <exception cref="InvalidOperationException"></exception>
  protected async Task<string> QueryDevice(AppSettings.Node node, string command, [CallerMemberName] string caller = "")
  {
    string result = "";

    SshClient? client = await GetConnection(node);

    if (client == null || !client.IsConnected) return "";

    Logger?.LogTrace("QueryDevice [{name}] command: {cmd}.", node.Name, command);

    using (SshCommand cmd = client.CreateCommand(command))
    {
      try
      {
        cmd.CommandTimeout = TimeSpan.FromSeconds(5);
        result = cmd.Execute();

        if (cmd.ExitStatus != 0)
        {
          // error
          throw new InvalidOperationException($"{caller} {node.Name} failed: [{cmd.ExitStatus}] {cmd.Error?.Trim().Replace('\n', '#')}.");
        }
        else
        {
          // success
          this.Logger?.LogTrace("{caller} {nodeName} result: {result}.", caller, node.Name, result?.Trim().Replace('\n', '#'));
        }
      }
      catch (Renci.SshNet.Common.SshOperationTimeoutException)
      {
        this.Logger?.LogWarning("{caller} [{name}] command: {cmd}: Command Timeout.", caller, node.Name, command);
      }
    }

    return result ?? "";
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!disposedValue)
    {
      if (disposing)
      {
        // dispose managed state (managed objects)
        foreach (var poolItem in this.ConnectionPool)
        {
          if (poolItem.Value?.IsConnected == true)
          {
            poolItem.Value?.Disconnect();
          }
          poolItem.Value?.Dispose();
        }

        this.ConnectionPool.Clear();
      }

      disposedValue = true;
    }
  }

  // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
  // ~Device()
  // {
  //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
  //     Dispose(disposing: false);
  // }

  public void Dispose()
  {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }
}
