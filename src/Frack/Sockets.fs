﻿// Taken from http://t0yv0.blogspot.com/2011/11/f-web-server-from-sockets-and-up.html
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
let asyncDo op prepare select =
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
                error (SocketIssue e)
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

let createTcpSocket() =
    new Socket(
        AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp,
        ReceiveBufferSize = 16384,
        SendBufferSize = 16384,
        NoDelay = false, // This disables nagle on true
        LingerState = LingerOption(true, 2))

/// Prepares the arguments by setting the buffer.
let inline setBuffer (buf: BS) (args: A) =
    args.SetBuffer(buf.Array, buf.Offset, buf.Count)

let private bytesPerLong = 4
let private bitsPerByte = 8

type Socket with
    member x.AsyncAccept () =
        asyncDo x.AcceptAsync ignore (fun a -> a.AcceptSocket)

    member x.AsyncAcceptSeq () =
        let rec loop () = asyncSeq {
            let! socket = x.AsyncAccept()
            yield socket
            yield! loop ()
        }
        loop ()

    member x.AsyncReceive (bufferPool: BufferPool) = async {
        let buf = bufferPool.Take()
        let! bytesRead = asyncDo x.ReceiveAsync (setBuffer buf) (fun a -> a.BytesTransferred)
        let chunk = BS(buf.Array.[buf.Offset..buf.Offset + bytesRead])
        bufferPool.Add(buf)
        return bytesRead, chunk
    }

    member x.AsyncReceiveSeq (bufferPool: BufferPool) =
        let rec loop () = asyncSeq {
            let! bytesRead, chunk = x.AsyncReceive(bufferPool)
            if bytesRead > 0 then
                yield chunk
                yield! loop ()
        }
        loop ()

    member x.AsyncSend (bs: BS, bufferPool: BufferPool) = async {
        let buf = bufferPool.Take()
        System.Buffer.BlockCopy(bs.Array, bs.Offset, buf.Array, buf.Offset, bs.Count)
        do! asyncDo x.SendAsync (setBuffer (BS(buf.Array, buf.Offset, buf.Count))) ignore
        bufferPool.Add(buf)
    }

    member x.AsyncSendSeq (data, bufferPool: BufferPool) =
        let rec loop data = async {
            let! chunk = data
            match chunk with
            | Cons(bs: BS, rest) ->
                do! x.AsyncSend(bs, bufferPool)
                do! loop rest
            | Nil -> ()
        }
        loop data

    member x.AsyncDisconnect () =
        asyncDo x.DisconnectAsync ignore <| fun a ->
            try
                x.Shutdown(SocketShutdown.Send)
            finally
                x.Close()
