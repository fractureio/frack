// Taken from http://t0yv0.blogspot.com/2011/11/f-web-server-from-sockets-and-up.html
module Frack.Sockets
#nowarn "40"

open System
open System.Net.Sockets
open Frack
open FSharp.Control
open FSharpx

type A = SocketAsyncEventArgs
type BS = ByteString

exception SocketIssue of SocketError with
    override this.ToString() = string this.Data0

/// Wraps the Socket.xxxAsync logic into F# async logic.
let inline asyncDo op prepare select =
    Async.FromContinuations <| fun (ok, error, cancel) ->
        let args = new A()
        prepare args
        let k (args: A) =
            match args.SocketError with
            | SocketError.Success ->
                let result = select args
                args.Dispose()
                ok result
            | e ->
                args.Dispose()
                error <| SocketIssue e
        let rec finish cont value =
            remover.Dispose()
            cont value
        and remover : IDisposable =
            args.Completed.Subscribe
                ({ new IObserver<_> with
                    member x.OnNext(v) = finish k v
                    member x.OnError(e) = finish error e
                    member x.OnCompleted() =
                        finish cancel <| System.OperationCanceledException("Cancelling the workflow, because the Observable awaited has completed.")
                })
        if not (op args) then
            finish k args

/// Prepares the arguments by setting the buffer.
let inline setBuffer (buf: BS) (args: A) =
    args.SetBuffer(buf.Array, buf.Offset, buf.Count)

let private bytesPerLong = 4
let private bitsPerByte = 8

type Socket with
    member x.AsyncAccept () =
        asyncDo x.AcceptAsync ignore (fun a -> a.AcceptSocket)

    member x.AsyncAcceptSeq (?receiveTimeout, ?sendTimeout) =
        let receiveTimeout = defaultArg receiveTimeout 1000
        let sendTimeout = defaultArg sendTimeout 1000
        let rec loop () = asyncSeq {
            let! socket = x.AsyncAccept()
            socket.ReceiveTimeout <- receiveTimeout
            socket.SendTimeout <- sendTimeout
            yield socket
            yield! loop ()
        }
        loop ()

    member x.AsyncReceive (buf: BS) =
        asyncDo x.ReceiveAsync (setBuffer buf) (fun a -> a.BytesTransferred)

    member x.AsyncReceiveSeq (bufferPool: BufferPool) =
        let rec loop () = asyncSeq {
            let buf = bufferPool.Take()
            let! bytesRead = x.AsyncReceive(buf)
            if bytesRead > 0 then
                let chunk = BS(buf.Array.[buf.Offset..buf.Offset + bytesRead])
                bufferPool.Add(buf)
                yield chunk
                yield! loop ()
            else bufferPool.Add(buf)
        }
        loop ()

    member x.AsyncSend (buf: BS) =
        asyncDo x.SendAsync (setBuffer buf) ignore

    member x.AsyncSendSeq (data, bufferPool: BufferPool) =
        let rec loop data = async {
            let! chunk = data
            match chunk with
            | Cons(bs: BS, rest) ->
                let buf = bufferPool.Take()
                System.Buffer.BlockCopy(bs.Array, bs.Offset, buf.Array, buf.Offset, bs.Count)
                do! x.AsyncSend(BS(buf.Array, buf.Offset, bs.Count))
                bufferPool.Add(buf)
                do! loop rest
            | Nil -> ()
        }
        loop data

    member x.AsyncDisconnect () =
        asyncDo x.DisconnectAsync ignore <| fun a ->
            try
                x.Shutdown(SocketShutdown.Both)
            finally
                x.Close()
