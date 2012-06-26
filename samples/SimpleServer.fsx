open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Threading

type Socket with
  member socket.AsyncAccept() = Async.FromBeginEnd(socket.BeginAccept, socket.EndAccept)
  member socket.AsyncReceive(buffer:byte[], ?offset, ?count) =
    let offset = defaultArg offset 0
    let count = defaultArg count buffer.Length
    let beginReceive(b,o,c,cb,s) = socket.BeginReceive(b,o,c,SocketFlags.None,cb,s)
    Async.FromBeginEnd(buffer, offset, count, beginReceive, socket.EndReceive)
  member socket.AsyncSend(buffer:byte[], ?offset, ?count) =
    let offset = defaultArg offset 0
    let count = defaultArg count buffer.Length
    let beginSend(b,o,c,cb,s) = socket.BeginSend(b,o,c,SocketFlags.None,cb,s)
    Async.FromBeginEnd(buffer, offset, count, beginSend, socket.EndSend)

type Server() =
  static member Start(hostname:string, ?port) =
    let ipAddress = Dns.GetHostEntry(hostname).AddressList.[0]
    Server.Start(ipAddress, ?port = port)

  static member Start(?ipAddress, ?port) =
    let ipAddress = defaultArg ipAddress IPAddress.Any
    let port = defaultArg port 80
    let endpoint = IPEndPoint(ipAddress, port)
    let cts = new CancellationTokenSource()
    let listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    listener.Bind(endpoint)
    listener.Listen(int SocketOptionName.MaxConnections)
    printfn "Started listening on port %d" port
    
    let rec loop() = async {
      printfn "Waiting for request ..."
      let! socket = listener.AsyncAccept()
      printfn "Received request"
      let response = @"HTTP/1.1 200 OK
Content-Type: text/plain
Content-Length: 13

Hello, world!"B
      try
        try
          let! bytesSent = socket.AsyncSend(response)
          printfn "Sent response"
        with e -> printfn "An error occurred: %s" e.Message
      finally
        socket.Shutdown(SocketShutdown.Both)
        socket.Close()
      return! loop() }

    Async.Start(loop(), cancellationToken = cts.Token)
    { new IDisposable with member x.Dispose() = cts.Cancel(); listener.Close() }

// Demo
let disposable = Server.Start(port = 8090)
System.Console.WriteLine("Listening on port 8090")
disposable.Dispose()
