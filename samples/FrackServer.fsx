#r @"..\packages\FSharpx.Core.1.6.1\lib\40\FSharpx.Core.dll"
#I @"..\src\Frack"
#load "Owin.fs"
#load "BufferPool.fs"
#load "Sockets.fs"
#load "Tcp.fs"
#load "Http.fs"

open FSharp.Control
open FSharpx
open Frack

let server = Http.Server(fun request -> async {
    System.Console.WriteLine("Request received")
    return {
        StatusCode = 200
        Headers = dict [| ("Content-Type", [|"text/plain"|]); ("Content-Length", [|"13"|]) |]
        Body = asyncSeq { yield BS"Hello, world!"B }
        Properties = dict [||]
    }
})

// Demo
let disposable = server.Start(port = 8090)
System.Console.WriteLine("Listening on port 8090")
disposable.Dispose()
