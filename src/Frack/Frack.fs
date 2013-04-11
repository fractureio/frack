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
namespace Frack

open System
open System.Collections.Generic
open System.IO
open System.Net.Sockets
open System.Text
open System.Threading
open System.Threading.Tasks
open Microsoft.FSharp.Core
open Frack.Sockets
open FSharp.Control
open FSharpx

type Env = Owin.Environment

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Request =
    [<CompiledName("ParseStartLine")>]
    let private parseStartLine (line: string, env: Env) =
        let arr = line.Split([|' '|], 3)
        env.[Owin.Constants.requestMethod] <- arr.[0]

        let uri = Uri(arr.[1], UriKind.RelativeOrAbsolute)

        // TODO: Fix this so that the path can be determined correctly.
        env.Add(Owin.Constants.requestPathBase, "")

        if uri.IsAbsoluteUri then
            env.[Owin.Constants.requestPath] <- uri.AbsolutePath
            env.[Owin.Constants.requestQueryString] <- uri.Query
            env.[Owin.Constants.requestScheme] <- uri.Scheme
            env.RequestHeaders.["Host"] <- [|uri.Host|]
        else
            env.[Owin.Constants.requestPath] <- uri.OriginalString

        env.[Owin.Constants.requestProtocol] <- arr.[2].Trim()

    [<CompiledName("ParseHeader")>]
    let private parseHeader (header: string, env: Env) =
        // TODO: Proper header parsing and aggregation, including linear white space.
        let pair = header.Split([|':'|], 2)
        if pair.Length > 1 then
            env.RequestHeaders.[pair.[0]] <- [| pair.[1].TrimStart(' ') |]

    [<CompiledName("Parse")>]
    let parse (readStream: SocketReadStream) =
        async {
            let env = new Env()
            // Do the parsing manually, as the reader is likely less efficient.
            use reader = new StreamReader(readStream, encoding = System.Text.Encoding.ASCII, detectEncodingFromByteOrderMarks = false, bufferSize = 4096, leaveOpen = true)
            let! requestLine = Async.AwaitTask <| reader.ReadLineAsync()
            parseStartLine(requestLine, env)
            let parsingRequestHeaders = ref true
            while !parsingRequestHeaders do
                if reader.EndOfStream then parsingRequestHeaders := false else
                // If not at the end of the stream, read the next line.
                // TODO: Account for linear white space.
                let! line = Async.AwaitTask <| reader.ReadLineAsync()
                if line = "" then
                    parsingRequestHeaders := false
                else parseHeader(line, env)
            env.Add(Owin.Constants.requestBody, readStream :> Stream)
            return env
        }

    [<CompiledName("ShouldKeepAlive")>]
    let shouldKeepAlive (env: Env) =
        let connection =
            if env.RequestHeaders <> null && env.RequestHeaders.Count > 0 && env.RequestHeaders.ContainsKey("Connection") then
                env.RequestHeaders.["Connection"]
            else Array.empty
        match string env.[Owin.Constants.requestProtocol] with
        | "HTTP/1.1" -> Array.isEmpty connection || not (connection |> Array.exists ((=) "Close"))
        | "HTTP/1.0" -> not (Array.isEmpty connection) && connection |> Array.exists ((=) "Keep-Alive")
        | _ -> false

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Response =
    open System.Net
    open System.Text

    [<CompiledName("GetStatusLine")>]
    let getStatusLine = function
      | 100 -> BS"HTTP/1.1 100 Continue\r\n"B
      | 101 -> BS"HTTP/1.1 101 Switching Protocols\r\n"B
      | 102 -> BS"HTTP/1.1 102 Processing\r\n"B
      | 200 -> BS"HTTP/1.1 200 OK\r\n"B
      | 201 -> BS"HTTP/1.1 201 Created\r\n"B
      | 202 -> BS"HTTP/1.1 202 Accepted\r\n"B
      | 203 -> BS"HTTP/1.1 203 Non-Authoritative Information\r\n"B
      | 204 -> BS"HTTP/1.1 204 No Content\r\n"B
      | 205 -> BS"HTTP/1.1 205 Reset Content\r\n"B
      | 206 -> BS"HTTP/1.1 206 Partial Content\r\n"B
      | 207 -> BS"HTTP/1.1 207 Multi-Status\r\n"B
      | 208 -> BS"HTTP/1.1 208 Already Reported\r\n"B
      | 226 -> BS"HTTP/1.1 226 IM Used\r\n"B
      | 300 -> BS"HTTP/1.1 300 Multiple Choices\r\n"B
      | 301 -> BS"HTTP/1.1 301 Moved Permanently\r\n"B
      | 302 -> BS"HTTP/1.1 302 Found\r\n"B
      | 303 -> BS"HTTP/1.1 303 See Other\r\n"B
      | 304 -> BS"HTTP/1.1 304 Not Modified\r\n"B
      | 305 -> BS"HTTP/1.1 305 Use Proxy\r\n"B
      | 306 -> BS"HTTP/1.1 306 Switch Proxy\r\n"B
      | 307 -> BS"HTTP/1.1 307 Temporary Redirect\r\n"B
      | 308 -> BS"HTTP/1.1 308 Permanent Redirect\r\n"B
      | 400 -> BS"HTTP/1.1 400 Bad Request\r\n"B
      | 401 -> BS"HTTP/1.1 401 Unauthorized\r\n"B
      | 402 -> BS"HTTP/1.1 402 Payment Required\r\n"B
      | 403 -> BS"HTTP/1.1 403 Forbidden\r\n"B
      | 404 -> BS"HTTP/1.1 404 Not Found\r\n"B
      | 405 -> BS"HTTP/1.1 405 Method Not Allowed\r\n"B
      | 406 -> BS"HTTP/1.1 406 Not Acceptable\r\n"B
      | 407 -> BS"HTTP/1.1 407 Proxy Authentication Required\r\n"B
      | 408 -> BS"HTTP/1.1 408 Request Timeout\r\n"B
      | 409 -> BS"HTTP/1.1 409 Conflict\r\n"B
      | 410 -> BS"HTTP/1.1 410 Gone\r\n"B
      | 411 -> BS"HTTP/1.1 411 Length Required\r\n"B
      | 412 -> BS"HTTP/1.1 412 Precondition Failed\r\n"B
      | 413 -> BS"HTTP/1.1 413 Request Entity Too Large\r\n"B
      | 414 -> BS"HTTP/1.1 414 Request-URI Too Long\r\n"B
      | 415 -> BS"HTTP/1.1 415 Unsupported Media Type\r\n"B
      | 416 -> BS"HTTP/1.1 416 Request Range Not Satisfiable\r\n"B
      | 417 -> BS"HTTP/1.1 417 Expectation Failed\r\n"B
      | 418 -> BS"HTTP/1.1 418 I'm a teapot\r\n"B
      | 422 -> BS"HTTP/1.1 422 Unprocessable Entity\r\n"B
      | 423 -> BS"HTTP/1.1 423 Locked\r\n"B
      | 424 -> BS"HTTP/1.1 424 Failed Dependency\r\n"B
      | 425 -> BS"HTTP/1.1 425 Unordered Collection\r\n"B
      | 426 -> BS"HTTP/1.1 426 Upgrade Required\r\n"B
      | 428 -> BS"HTTP/1.1 428 Precondition Required\r\n"B
      | 429 -> BS"HTTP/1.1 429 Too Many Requests\r\n"B
      | 431 -> BS"HTTP/1.1 431 Request Header Fields Too Large\r\n"B
      | 451 -> BS"HTTP/1.1 451 Unavailable For Legal Reasons\r\n"B
      | 500 -> BS"HTTP/1.1 500 Internal Server Error\r\n"B
      | 501 -> BS"HTTP/1.1 501 Not Implemented\r\n"B
      | 502 -> BS"HTTP/1.1 502 Bad Gateway\r\n"B
      | 503 -> BS"HTTP/1.1 503 Service Unavailable\r\n"B
      | 504 -> BS"HTTP/1.1 504 Gateway Timeout\r\n"B
      | 505 -> BS"HTTP/1.1 505 HTTP Version Not Supported\r\n"B
      | 506 -> BS"HTTP/1.1 506 Variant Also Negotiates\r\n"B
      | 507 -> BS"HTTP/1.1 507 Insufficient Storage\r\n"B
      | 508 -> BS"HTTP/1.1 508 Loop Detected\r\n"B
      | 509 -> BS"HTTP/1.1 509 Bandwidth Limit Exceeded\r\n"B
      | 510 -> BS"HTTP/1.1 510 Not Extended\r\n"B
      | 511 -> BS"HTTP/1.1 511 Network Authentication Required\r\n"B
      | _ -> BS"HTTP/1.1 418 I'm a teapot\r\n"B

    [<CompiledName("Send")>]
    let send (env: Env, writeStream: SocketWriteStream) = async {
        // TODO: Aggregate the response pieces and send as a whole chunk.
        // Write the status line
        let statusLine = getStatusLine <| Convert.ToInt32(env.[Owin.Constants.responseStatusCode])
        do! writeStream.AsyncWrite(statusLine.Array, statusLine.Offset, statusLine.Count)

        // Write the headers
        for (KeyValue(header, values)) in env.ResponseHeaders do
            for value in values do
                let headerBytes = ByteString.ofString <| sprintf "%s: %s\r\n" header value
                do! writeStream.AsyncWrite(headerBytes.Array, headerBytes.Offset, headerBytes.Count)

        // Write the body separator
        do! writeStream.AsyncWrite("\r\n"B, 0, 2)

        // Write the response body
        // TODO: Set a default timeout
        let body = env.ResponseBody.ToArray()
        let! _ = Async.AwaitIAsyncResult <| writeStream.WriteAsync(body, 0, body.Length)
        return ()
    }
