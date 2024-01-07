This folder is a good place to copy your SSL key files.  You will need key files for your nodes, and for the Turing Pi.  After 
copying your SSH key files here, edit appSettings.json and update the TuringPi and Nodes sections.

"Settings": {
  "TuringPi": {
      "name": "TuringPi",
      "hostname": "turingpi.local",
      "username": "root",
      "keyfile": "Keys/turing.key"      // this is your turing BMC SSH key
    },

 "Nodes": [
      {
        "name": "node 1",
        "hostname": "node1.local",
        "username": "pi-admin",
        "keyfile": "Keys/node1.key"     // each node has a SSH key file
      }
  ]
}