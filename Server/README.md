# King's Court server

Serves the WebGL build and relays messages between phones by 4-letter join code. It has no game logic:
the host phone runs the rules and decides what is legal.

## Run it

    cd Server
    npm install        (first time only)
    node server.js     (serves ../Builds/WebGL on port 8000)

It prints the address phones should open, for example `http://192.168.1.20:8000`.
Phones must be on the same Wi-Fi as this PC. The first time, Windows asks whether Node.js may accept
connections: choose **Private networks**.

`node server.js <folder>` serves a different build folder. `PORT=9000 node server.js` changes the port.

## Test it

    node test.js       (protocol tests for the relay)

The Unity Play Mode tests (Assets/Tests/PlayMode) also start this server and play whole games over real sockets.
