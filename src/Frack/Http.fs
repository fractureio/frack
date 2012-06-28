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

open System.Net
open Frack
open Frack.Sockets

type Server (app, ?backlog, ?bufferSize) =
    let backlog = defaultArg backlog 1000
    let bufferSize = defaultArg bufferSize 4096

    member x.Start(hostname: string, ?port) =
        let ipAddress = Dns.GetHostEntry(hostname).AddressList.[0]
        x.Start(ipAddress, ?port = port)

    member x.Start(?ipAddress, ?port) = 
        let pool = BufferPool(backlog, bufferSize)

        let tcp = Tcp.Server(fun socket -> async {
            let! request = Request.parse <| socket.ReceiveAsyncSeq(pool)
            let keepAlive = Request.shouldKeepAlive request
            let! response = app request
            do! socket.SendAsyncSeq(Response.toBytes response, pool)
            return keepAlive
        }, backlog)

        tcp.Start(?ipAddress = ipAddress, ?port = port)
