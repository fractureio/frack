module Frack.Sockets

open System.Net.Sockets
open Frack
open FSharp.Control
open FSharpx

type A = SocketAsyncEventArgs

exception SocketIssue of SocketError with
    override this.ToString() = string this.Data0

/// Wraps the Socket.xxxAsync logic into F# async logic.
let inline asyncDo (op: A -> bool) (prepare: A -> unit) (select: A -> 'T) =
    Async.FromContinuations <| fun (ok, error, _) ->
        let args = new A()
        prepare args
        let k (args: A) =
            match args.SocketError with
            | System.Net.Sockets.SocketError.Success ->
                let result = select args
                args.Dispose()
                ok result
            | e ->
                args.Dispose()
                error (SocketIssue e)
        args.add_Completed(System.EventHandler<_>(fun _ -> k))
        if not (op args) then
            k args

/// Prepares the arguments by setting the buffer.
let inline setBuffer (buf: BS) (args: A) =
    args.SetBuffer(buf.Array, buf.Offset, buf.Count)

type Socket with
    member x.AsyncAccept () =
        asyncDo x.AcceptAsync ignore (fun a -> a.AcceptSocket)

    member x.AcceptAsyncSeq () =
        let rec loop () = asyncSeq {
            let! socket = x.AsyncAccept()
            yield socket
            yield! loop ()
        }
        loop ()

    member x.AsyncReceive (buf: BS) =
        asyncDo x.ReceiveAsync (setBuffer buf) (fun a -> a.BytesTransferred)

    member x.ReceiveAsyncSeq (pool: BufferPool) =
        let rec loop () = asyncSeq {
            let! buf = pool.Pop()
            let! bytesRead = x.AsyncReceive(buf)
            if bytesRead > 0 then
                yield buf
                do! pool.Push(buf)
                yield! loop ()
            else
                do! pool.Push(buf)
                ()
        }
        loop ()

    member x.AsyncSend (buf: BS) =
        asyncDo x.SendAsync (setBuffer buf) ignore

    member x.SendAsyncSeq (data, pool: BufferPool) =
        let rec loop data = async {
            let! chunk = data
            match chunk with
            | Cons(bs: BS, rest) ->
                let! buf = pool.Pop()
                Array.blit bs.Array bs.Offset buf.Array buf.Offset bs.Count
                do! x.AsyncSend(buf)
                do! pool.Push(buf)
                do! loop rest
            | Nil -> do! x.AsyncSend(BS())
        }
        loop data
