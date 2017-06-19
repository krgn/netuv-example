module NetUv.Client

open System
open System.Net
open System.Runtime.InteropServices
open System.Text
open NetUV.Core.Buffers
open NetUV.Core.Handles
open NetUV.Core.Logging


let Port = 9988

let mutable loop:Loop = Unchecked.defaultof<Loop>

let getPipeName() =
  if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
    "\\\\?\\pipe\\echo"
  else
    "/tmp/echo"

type ServerType =
  | Tcp
  | Pipe
  | Udp

let onClosed =
  Action<_>(fun (handle: 't when 't :> IDisposable) -> handle.Dispose())

let onError =
  Action<_,_>(fun (stream: StreamHandle) (error: Exception) ->
    printfn "Echo client read error %O" error)

let onReceive  =
  Action<_,_>(fun (udp: Udp) (completion: IDatagramReadCompletion ) ->
    if not (isNull completion.Error) then
      printfn "Echo client receive error %A" completion.Error

    let remoteEndPoint = completion.RemoteEndPoint
    let data:ReadableBuffer = completion.Data
    let message:string  = data.ReadString(Encoding.UTF8)
    printfn "Echo client received : %s from %O" message remoteEndPoint
    udp.CloseHandle(onClosed))

let onSendCompleted =
  Action<_,_>(fun (udp: Udp) (exn: Exception) ->
    if not (isNull exn) then
      printfn "Echo server send error %O" exn
      udp.CloseHandle(onClosed))

let createMessage() =
  let array = Encoding.UTF8.GetBytes(sprintf "Greetings %O" DateTime.UtcNow)
  let buffer: WritableBuffer  = WritableBuffer.From(array)
  buffer

let onWriteCompleted (stream: StreamHandle) (error: Exception) =
  if not (isNull error) then
    printfn "Echo client write error %O" error
    stream.CloseHandle(onClosed)

let onAccept  =
  Action<_,_>(fun (stream: StreamHandle) (data: ReadableBuffer) ->
    if data.Count <> 0 then
      let message = data.ReadString(Encoding.UTF8)
      data.Dispose()
      printfn "Echo client received : %s" message

      printfn "Message received, sending QS to server"
      let array = Encoding.UTF8.GetBytes("QS")
      let buffer: WritableBuffer = WritableBuffer.From(array)
      stream.QueueWriteStream(buffer, onWriteCompleted))

let onConnected =
  Action<_,_>(fun (client: 't when 't :> StreamHandle) (exn: Exception) ->
    if not (isNull exn) then
      printfn "%s:Echo client error %O" (typeof<'t>.Name) exn
      client.CloseHandle(onClosed)
    else
      printfn "%s:Echo client connected, request write message." (typeof<'t>.Name)
      client.OnRead(onAccept, onError)

      let buffer:WritableBuffer  = createMessage()
      client.QueueWriteStream(buffer, onWriteCompleted))

let runLoop (serverType: ServerType) =
  loop <- new Loop()

  let localEndPoint = IPEndPoint(IPAddress.Any, IPEndPoint.MinPort)
  let remoteEndPoint = IPEndPoint(IPAddress.Loopback, Port)

  let mutable handle: IDisposable  = Unchecked.defaultof<IDisposable>

  match serverType with
  | ServerType.Udp ->
    let udp = loop.CreateUdp().ReceiveStart(onReceive)
    let buffer:WritableBuffer = createMessage()
    udp.QueueSend(buffer, remoteEndPoint, onSendCompleted)
    handle <- udp
  | ServerType.Pipe ->
    let name: string = getPipeName()
    handle <- loop.CreatePipe().ConnectTo(name, onConnected)

  | ServerType.Tcp ->
    handle <- loop.CreateTcp().NoDelay(true).ConnectTo(localEndPoint, remoteEndPoint, onConnected)

  printfn "%A:Echo client loop starting." serverType
  loop.RunDefault() |> printfn "result: %d"
  printfn "%A:Echo client loop dropped out" serverType
  if not (isNull handle) then handle.Dispose()

[<EntryPoint>]
let main argv =
  LogFactory.AddConsoleProvider()

  try
    runLoop Tcp

    loop.Dispose()
  with
    | exn ->
      printfn "Exception: %s" exn.Message
      printfn "%s" exn.StackTrace

  Console.ReadLine() |> ignore
  0 // return an integer exit code
