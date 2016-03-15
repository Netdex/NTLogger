# NTLogger
A networked LAN keylogger written in C#.

This project has two components: NTLogger and NTLogger Server. NTLogger runs on the host, and NTLogger Server runs on the remote machine being surveilled.

## Function

NTLogger Server waits for a UDP broadcast containing the the host's IP address. This broadcast is on multicast subnet 224.5.6.7. After the UDP broadcast is received, the client establishes a TCP connection with the host, and continuously sends keyboard output from the client to the host until the client is closed.

If the host loses connection for any reason, the client resets and begins listening for UDP broadcasts again on the same multicast address.
