// Taken from http://t0yv0.blogspot.com/2011/11/f-web-server-from-sockets-and-up.html
module Frack.Sockets
#nowarn "40"

open System
open System.Net.Sockets
open Fracture
open FSharp.Control
open FSharpx

type A = SocketAsyncEventArgs
type B = BocketPool
type BS = ByteString

/// Wraps the Socket.xxxAsync logic into F# async logic.
let inline asyncDo op (args: A) select =
    Async.FromContinuations <| fun (ok, error, cancel) ->
        let callback (args: A) =
            if args.SocketError = SocketError.Success then
                let result = select args
                args.Dispose()
                ok result
            else
                args.Dispose()
                error (SocketException(int args.SocketError))
        args.Completed.Add(callback)
        if not (op args) then
            callback args

let [<Literal>] private bytesPerLong = 4
let [<Literal>] private bitsPerByte = 8
let [<Literal>] private defaultTimeout = 1000

type Socket with
    member x.AsyncAccept args = asyncDo x.AcceptAsync args (fun a -> a.AcceptSocket)

    member x.AsyncAcceptSeq (pool: B, ?receiveTimeout, ?sendTimeout) =
        let receiveTimeout = defaultArg receiveTimeout defaultTimeout
        let sendTimeout = defaultArg sendTimeout defaultTimeout
        let rec loop () = asyncSeq {
            let args = pool.CheckOut()
            let! socket = x.AsyncAccept(args)
            pool.CheckIn(args)
            socket.ReceiveTimeout <- receiveTimeout
            socket.SendTimeout <- sendTimeout
            yield socket
            yield! loop ()
        }
        loop ()

    member x.AsyncReceive args = asyncDo x.ReceiveAsync args (fun a -> a.BytesTransferred)

    member x.AsyncReceiveSeq (pool: B) =
        let rec loop () = asyncSeq {
            let args = pool.CheckOut()
            let! bytesRead = x.AsyncReceive(args)
            if bytesRead > 0 then
                let chunk =
                    if bytesRead < args.Count then
                        BS(args.Buffer.[args.Offset..(args.Offset+bytesRead-1)])
                    else BS(args.Buffer)
                pool.CheckIn(args)
                yield chunk
                yield! loop ()
            else pool.CheckIn(args)
        }
        loop ()

    member x.AsyncSend args = asyncDo x.SendAsync args ignore

    member x.AsyncSendSeq (data, pool: B) =
        let rec loop data = async {
            let! chunk = data
            match chunk with
            | Cons(bs: BS, rest) ->
                let args = pool.CheckOut()
                System.Buffer.BlockCopy(bs.Array, bs.Offset, args.Buffer, args.Offset, bs.Count)
                do! x.AsyncSend(args)
                pool.CheckIn(args)
                do! loop rest
            | Nil -> ()
        }
        loop data

    member x.AsyncDisconnect (pool: B) =
        let args = pool.CheckOut()
        asyncDo x.DisconnectAsync args <| fun a ->
            try
                x.Shutdown(SocketShutdown.Both)
            finally
                pool.CheckIn(args)
                x.Close()
