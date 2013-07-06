module Frack.Sockets
#nowarn "40"

open System
open System.Net
open System.Net.Sockets
open Fracture

// See http://t0yv0.blogspot.com/2011/11/f-web-server-from-sockets-and-up.html
type A = SocketAsyncEventArgs
type B = BocketPool

/// Wraps the Socket.xxxAsync logic into F# async logic.
let inline asyncDo op (args: A) select =
    Async.FromContinuations <| fun (ok, error, cancel) ->
        let callback (args: A) =
            if args.SocketError = SocketError.Success then
                let result = select args
                ok result
            else
                error (SocketException(int args.SocketError))
        args.Completed.Add(callback)
        if not (op args) then
            callback args

type Socket with
    member x.AsyncAccept args = asyncDo x.AcceptAsync args (fun a -> a.AcceptSocket)
    member x.AsyncReceive args = asyncDo x.ReceiveAsync args (fun a -> a.BytesTransferred)
    member x.AsyncSend args = asyncDo x.SendAsync args ignore
    member x.AsyncDisconnect args =
        asyncDo x.DisconnectAsync args <| fun a -> x.Shutdown(SocketShutdown.Both)

/// A read-only stream wrapping a `Socket`.
/// This stream does not close or dispose the underlying socket.
type SocketReadStream(socket: Socket, pool: B) as x =
    inherit System.IO.Stream()
    do if socket = null then
        raise <| ArgumentNullException()
    do if not (socket.Blocking) then
        raise <| System.IO.IOException()
    do if not (socket.Connected) then
        raise <| System.IO.IOException()
    do if socket.SocketType <> SocketType.Stream then
        raise <| System.IO.IOException()

    // TODO: This type should probably also accept an additional, optional buffer as the first bit of data returned from a `Read`.
    // This would allow something like an HTTP parser to restore to the `SocketReadStream` any bytes read that were not part of
    // the HTTP headers.

    let mutable readTimeout = 1000
    let beginRead, endRead, _ = Async.AsBeginEnd(fun (b, o, c) -> x.AsyncRead(b, o, c))

    /// Reads `count` bytes from the specified `buffer` starting at the specified `offset` asynchronously.
    /// It's possible that the caller is providing a different buffer, offset, or count.
    /// The `BocketPool` is highly optimized to use a single, large buffer and limit
    /// extraneous calls to methods such as `SocketAsyncEventArgs.SetBuffer`. When possible,
    /// the caller should provide the same values as were used to create the `BocketPool`.
    member x.AsyncRead(buffer: byte[], offset, count) =
        if count = 0 then async.Return 0 else

        // TODO: This type should retain the `args` whenever the caller's offset + count does not retrieve the bytes
        // stored in the `args`. Subsequent calls should then first read from the previous args, then retrieve the next.

        let bocketPool = pool
        async {
            let args = bocketPool.CheckOut()
            let! bytesRead = socket.AsyncReceive(args)
            if buffer <> args.Buffer || offset <> args.Offset || count <> args.Count then
                Buffer.BlockCopy(args.Buffer, args.Offset, buffer, offset, bytesRead)
            args.UserToken <- Unchecked.defaultof<_>
            bocketPool.CheckIn(args)
            return bytesRead
        }

    override x.CanRead = true
    override x.CanSeek = false
    override x.CanWrite = false
    override x.Flush() = raise <| NotSupportedException()
    override x.Length = raise <| NotSupportedException()
    override x.Position
        with get() = raise <| NotSupportedException()
        and set(v) = raise <| NotSupportedException()
    override x.ReadTimeout
        with get() = readTimeout
        and set(v) = readTimeout <- v
    override x.Seek(offset, origin) = raise <| NotSupportedException()
    override x.SetLength(value) = raise <| NotSupportedException()
    override x.Read(buffer, offset, count) =
        if count = 0 then 0 else
        socket.Receive(buffer, offset, count, SocketFlags.None)
    override x.Write(buffer, offset, count) = raise <| NotSupportedException()

    // Async Stream methods
    override x.ReadAsync(buffer, offset, count, cancellationToken) =
        Async.StartAsTask(
            x.AsyncRead(buffer, offset, count),
            cancellationToken = cancellationToken)
    override x.BeginRead(buffer, offset, count, callback, state) =
        beginRead((buffer, offset, count), callback, state)
    override x.EndRead(asyncResult) = endRead(asyncResult)

/// A write-only stream wrapping a `Socket`.
/// This stream does not close or dispose the underlying socket.
type SocketWriteStream(socket: Socket, pool: B) as x =
    inherit System.IO.Stream()
    do if socket = null then
        raise <| ArgumentNullException()
    do if not (socket.Blocking) then
        raise <| System.IO.IOException()
    do if not (socket.Connected) then
        raise <| System.IO.IOException()
    do if socket.SocketType <> SocketType.Stream then
        raise <| System.IO.IOException()
    
    let mutable writeTimeout = 1000
    let beginWrite, endWrite, _ = Async.AsBeginEnd(fun (b, o, c) -> x.AsyncWrite(b, o, c))

    /// Asynchronously writes `count` bytes to the `socket` starting at the specified `offset`
    /// from the provided `buffer`.
    member x.AsyncWrite(buffer: byte[], offset, count) =
        if buffer = null then
            raise <| ArgumentNullException()
        if offset < 0 || offset >= buffer.Length then
            raise <| ArgumentOutOfRangeException("offset")
        if count < 0 || count > buffer.Length - offset then
            raise <| ArgumentOutOfRangeException("count")

        if count = 0 then async.Zero () else
        socket.Send(buffer, offset, count, SocketFlags.None) |> ignore
        async.Return ()

        // TODO: Fix async sending
//        let bocketPool = pool
//        async {
//            let args = bocketPool.CheckOut()
//            // Copy the data into the args buffer.
//            Buffer.BlockCopy(buffer, offset, args.Buffer, args.Offset, count)
//            // TODO: Handle the case where the args.Count is too small
//
//            // Set the count so that the correct number of bytes are sent.
//            // TODO: Only reset the buffer if necessary.
//            let origBuffer = args.Buffer
//            let origOffset = args.Offset
//            let origCount = args.Count
//            args.SetBuffer(origBuffer, origOffset, count)
//
//            do! socket.AsyncSend(args)
//
//            // Reset the count for the args.
//            // TODO: Only reset the buffer if necessary.
//            args.SetBuffer(origBuffer, origOffset, origCount)
//
//            args.UserToken <- Unchecked.defaultof<_>
//            bocketPool.CheckIn(args)
//        }

    override x.CanRead = false
    override x.CanSeek = false
    override x.CanWrite = true
    override x.Flush() = raise <| NotSupportedException()
    override x.Length = raise <| NotSupportedException()
    override x.Position
        with get() = raise <| NotSupportedException()
        and set(v) = raise <| NotSupportedException()
    override x.Seek(offset, origin) = raise <| NotSupportedException()
    override x.WriteTimeout
        with get() = writeTimeout
        and set(v) = writeTimeout <- v
    override x.SetLength(value) = raise <| NotSupportedException()
    override x.Read(buffer, offset, count) = raise <| NotSupportedException()
    override x.Write(buffer, offset, count) =
        if buffer = null then
            raise <| ArgumentNullException()
        if offset < 0 || offset >= buffer.Length then
            raise <| ArgumentOutOfRangeException("offset")
        if count < 0 || count > buffer.Length - offset then
            raise <| ArgumentOutOfRangeException("count")

        if count = 0 then () else
        socket.Send(buffer, offset, count, SocketFlags.None) |> ignore

    // Async Stream methods
    override x.WriteAsync(buffer, offset, count, cancellationToken) =
        Async.StartAsTask(
            x.AsyncWrite(buffer, offset, count),
            cancellationToken = cancellationToken
        )
        :> System.Threading.Tasks.Task
    override x.BeginWrite(buffer, offset, count, callback, state) =
        beginWrite((buffer, offset, count), callback, state)
    override x.EndWrite(asyncResult) = endWrite(asyncResult)
