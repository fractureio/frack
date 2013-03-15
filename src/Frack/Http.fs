//----------------------------------------------------------------------------
//
// Copyright (c) 2011-2012 Ryan Riley (@panesofglass)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//----------------------------------------------------------------------------
module Frack.Http

open System
open System.Net
open System.Net.Sockets
open Frack
open Frack.Sockets
open Fracture

type Server (app, ?backlog, ?bufferSize) =
    let backlog = defaultArg backlog 1000
    let bufferSize = defaultArg bufferSize 4096

    let rec run pool (socket: Socket) = async {
        let! request = Request.parse <| socket.AsyncReceiveSeq(pool)
        let! response = app request
        do! socket.AsyncSendSeq(Response.toBytes response, pool)
//        if Request.shouldKeepAlive request then
//            return! run pool socket
    }

    member x.Start(hostname: string, ?port, ?maxPoolCount, ?perBocketBufferSize) =
        let ipAddress = Dns.GetHostEntry(hostname).AddressList.[0]
        x.Start(ipAddress, ?port = port, ?maxPoolCount = maxPoolCount, ?perBocketBufferSize = perBocketBufferSize)

    member x.Start(?ipAddress, ?port, ?maxPoolCount, ?perBocketBufferSize) = 
        let maxPoolCount = defaultArg maxPoolCount 30000
        let perBocketBufferSize = defaultArg perBocketBufferSize 4096
        let pool = new BocketPool("sendreceive", maxPoolCount, perBocketBufferSize)
        let tcp = Tcp.Server(run pool, backlog)
        let disposable = tcp.Start(?ipAddress = ipAddress, ?port = port)
        { new IDisposable with
            member x.Dispose() =
                disposable.Dispose()
                pool.Dispose()
        }
