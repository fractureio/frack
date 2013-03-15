﻿//----------------------------------------------------------------------------
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
namespace Owin

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Microsoft.FSharp.Core
open FSharp.Control

(**
 * OWIN 1.0.0
 * http://owin.org/spec/owin-1.0.0.html
 *)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Constants =
    (* 3.2.1 Request Data *)
    [<CompiledName("RequestScheme")>]
    let [<Literal>] requestScheme = "owin.RequestScheme"
    [<CompiledName("RequestMethod")>]
    let [<Literal>] requestMethod = "owin.RequestMethod"
    [<CompiledName("RequestPathBase")>]
    let [<Literal>] requestPathBase = "owin.RequestPathBase"
    [<CompiledName("RequestPath")>]
    let [<Literal>] requestPath = "owin.RequestPath"
    [<CompiledName("RequestQueryString")>]
    let [<Literal>] requestQueryString = "owin.RequestQueryString"
    [<CompiledName("RequestProtocol")>]
    let [<Literal>] requestProtocol = "owin.RequestProtocol"
    [<CompiledName("RequestHeaders")>]
    let [<Literal>] requestHeaders = "owin.RequestHeaders"
    [<CompiledName("RequestBody")>]
    let [<Literal>] requestBody = "owin.RequestBody"

    (* 3.2.2 Response Data *)
    [<CompiledName("ResponseStatusCode")>]
    let [<Literal>] responseStatusCode = "owin.ResponseStatusCode"
    [<CompiledName("ResponseReasonPhrase")>]
    let [<Literal>] responseReasonPhrase = "owin.ResponseReasonPhrase"
    [<CompiledName("ResponseProtocol")>]
    let [<Literal>] responseProtocol = "owin.ResponseProtocol"
    [<CompiledName("ResponseHeaders")>]
    let [<Literal>] responseHeaders = "owin.ResponseHeaders"
    [<CompiledName("ResponseBody")>]
    let [<Literal>] responseBody = "owin.ResponseBody"

    (* 3.2.3 Other Data *)
    [<CompiledName("CallCancelled")>]
    let [<Literal>] callCancelled = "owin.CallCancelled"
    [<CompiledName("OwinVersion")>]
    let [<Literal>] owinVersion = "owin.Version"

    (* http://owin.org/spec/CommonKeys.html *)
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module CommonKeys =
        [<CompiledName("ClientCertificate")>]
        let [<Literal>] clientCertificate = "ssl.ClientCertificate"
        [<CompiledName("RemoteIpAddress")>]
        let [<Literal>] remoteIpAddress = "server.RemoteIpAddress"
        [<CompiledName("RemotePort")>]
        let [<Literal>] remotePort = "server.RemotePort"
        [<CompiledName("LocalIpAddress")>]
        let [<Literal>] localIpAddress = "server.LocalIpAddress"
        [<CompiledName("LocalPort")>]
        let [<Literal>] localPort = "server.LocalPort"
        [<CompiledName("IsLocal")>]
        let [<Literal>] isLocal = "server.IsLocal"
        [<CompiledName("TraceOutput")>]
        let [<Literal>] traceOutput = "host.TraceOutput"
        [<CompiledName("Addresses")>]
        let [<Literal>] addresses = "host.Addresses"
        [<CompiledName("Capabilities")>]
        let [<Literal>] capabilities = "server.Capabilities"
        [<CompiledName("OnSendingHeaders")>]
        let [<Literal>] onSendingHeaders = "server.OnSendingHeaders"

    (* http://owin.org/extensions/owin-SendFile-Extension-v0.3.0.htm *)
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module SendFiles =
        // 3.1. Startup
        [<CompiledName("Version")>]
        let [<Literal>] version = "sendfile.Version"
        [<CompiledName("Support")>]
        let [<Literal>] support = "sendfile.Support"
        [<CompiledName("Concurrency")>]
        let [<Literal>] concurrency = "sendfile.Concurrency"

        // 3.2. Per Request
        [<CompiledName("SendAsync")>]
        let [<Literal>] sendAsync = "sendfile.SendAsync"

    (* http://owin.org/extensions/owin-OpaqueStream-Extension-v0.3.0.htm *)
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module Opaque =
        // 3.1. Startup
        [<CompiledName("Version")>]
        let [<Literal>] version = "opaque.Version"

        // 3.2. Per Request
        [<CompiledName("Upgrade")>]
        let [<Literal>] upgrade = "opaque.Upgrade"

        // 5. Consumption
        [<CompiledName("Stream")>]
        let [<Literal>] stream = "opaque.Stream"
        [<CompiledName("CallCanceled")>]
        let [<Literal>] callCancelled = "opaque.CallCancelled"

    // http://owin.org/extensions/owin-OpaqueStream-Extension-v0.3.0.htm
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module WebSocket =
        // 3.1. Startup
        [<CompiledName("Version")>]
        let [<Literal>] version = "websocket.Version"

        // 3.2. Per Request
        [<CompiledName("Accept")>]
        let [<Literal>] accept = "websocket.Accept"

        // 4. Accept
        [<CompiledName("SubProtocol")>]
        let [<Literal>] subProtocol = "websocket.SubProtocol"

        // 5. Consumption
        [<CompiledName("SendAsync")>]
        let [<Literal>] sendAsync = "websocket.SendAsync"
        [<CompiledName("ReceiveAsync")>]
        let [<Literal>] receiveAsync = "websocket.ReceiveAsync"
        [<CompiledName("CloseAsync")>]
        let [<Literal>] closeAsync = "websocket.CloseAsync"
        [<CompiledName("CallCancelled")>]
        let [<Literal>] callCancelled = "websocket.CallCancelled"
        [<CompiledName("ClientCloseStatus")>]
        let [<Literal>] clientCloseStatus = "websocket.ClientCloseStatus"
        [<CompiledName("ClientCloseDescription")>]
        let [<Literal>] clientCloseDescription = "websocket.ClientCloseDescription"

type OwinApp = IDictionary<string, obj> -> Task

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module OwinApp =
    [<CompiledName("ToFunc")>]
    let toFunc (app: OwinApp) = Func<_,_>(app)
