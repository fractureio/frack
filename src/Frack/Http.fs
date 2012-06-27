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
open System.Collections.Generic
open System.Diagnostics.Contracts
open System.IO
open System.Net
open System.Text
open FSharp.Control
open FSharp.IO
open FSharpx
open FSharpx.Iteratee
open FSharpx.Iteratee.Binary
open Frack
open Frack.Sockets
open Owin

module Parser =

    let readHeaders =
        let rec lines cont = readLine >>= fun bs -> skipNewline >>= check cont bs
        and check cont bs count =
            match bs, count with
            | bs, _ when ByteString.isEmpty bs -> Done(cont [], EOF)
            | bs, 0 -> Done(cont [bs], EOF)
            | _ -> lines <| fun tail -> cont <| bs::tail
        lines id

    let private parseStartLine (line: string, request: Request) =
        let arr = line.Split([|' '|], 3)
        request.Environment.Add(Request.Version, "1.0")
        request.Environment.Add(Request.Method, arr.[0])

        let uri = Uri(arr.[1], UriKind.RelativeOrAbsolute)
        request.Environment.Add("frack.RequestUri", uri)

        // TODO: Fix this so that the path can be determined correctly.
        request.Environment.Add(Request.PathBase, "")

        if uri.IsAbsoluteUri then
            request.Environment.Add(Request.Path, uri.AbsolutePath)
            request.Environment.Add(Request.QueryString, uri.Query)
            request.Environment.Add(Request.Scheme, uri.Scheme)
            request.Headers.Add("Host", [|uri.Host|])
        else
            request.Environment.Add(Request.Path, uri.OriginalString)

        let version = Version.Parse(arr.[2].TrimStart("HTP/".ToCharArray()))
        request.Environment.Add("frack.HttpVersion", version)

    let private parseHeader (header: string, request: Owin.Request) =
        // TODO: Proper header parsing.
        let pair = header.Split([|':'|], 2)
        if pair.Length > 1 then
            request.Headers.[pair.[0]] <- [| pair.[1].TrimStart(' ') |]

    let parse source = async {
        let! result, source' = source |> connect readHeaders
        match result with
        | [] -> return Request.None
        | startLine::headers ->
            let request = {
                Environment = new Dictionary<_,_>(HashIdentity.Structural)
                Headers = new Dictionary<_,_>(HashIdentity.Structural)
                Body = source'
            }
            parseStartLine (startLine |> ByteString.toString, request)
            headers |> List.iter (fun h -> parseHeader(h |> ByteString.toString, request))
            return request
    }

type Server (app, ?backlog, ?bufferSize) =

    let backlog = defaultArg backlog 1000
    let bufferSize = defaultArg bufferSize 4096
    let pool = BufferPool(backlog, bufferSize)

    let tcp = Tcp.Server(fun socket -> async {
        Console.WriteLine("socket received")
        let! request = Parser.parse <| socket.ReceiveAsyncSeq(pool)
        Console.WriteLine("request parsed")
        let! response = app request
        Console.WriteLine("sending response")
        do! socket.SendAsyncSeq(Response.toBytes response, pool)
    }, backlog)

    member x.Start(hostname: string, ?port) = tcp.Start(hostname, ?port = port)
    member x.Start(?ipAddress, ?port) = tcp.Start(?ipAddress = ipAddress, ?port = port)
