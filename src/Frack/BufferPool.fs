namespace Frack

open System.Collections.Concurrent
open FSharpx

type BS = ByteString

type BufferPool(totalBuffers: int, bufferSize) =
    let buffer = Array.zeroCreate<byte> (totalBuffers * bufferSize)
    let queue = new BlockingCollection<BS>(totalBuffers)
    do for i in 0 .. totalBuffers - 1 do
        let bs = BS(buffer, bufferSize * i, bufferSize)
        queue.Add(bs)

    member x.Take() = queue.Take()
    member x.Add(bs) = queue.Add(bs)
