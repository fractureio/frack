namespace Frack

open FSharp.Control
open FSharpx

type BS = ByteString

type BufferPool(totalBuffers, bufferSize) =
    let buffer = Array.zeroCreate<byte> (totalBuffers * bufferSize)
    let queue = FSharp.Control.BlockingQueueAgent<BS>(totalBuffers)
    do for i in 0 .. totalBuffers - 1 do
        let bs = BS(buffer, bufferSize * i, bufferSize * (i + 1))
        queue.Add(bs)

    member x.Pop(?timeout) = queue.AsyncGet(?timeout = timeout)
    member x.Push(bs, ?timeout) = queue.AsyncAdd(bs, ?timeout = timeout)
