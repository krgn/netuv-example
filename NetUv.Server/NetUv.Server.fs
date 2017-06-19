module NetUv.Server

open System
open System.Net
open System.Runtime.InteropServices
open System.Text
open NetUV.Core.Buffers
open NetUV.Core.Handles
open NetUV.Core.Logging

let port = 9988
let endPoint = IPEndPoint(IPAddress.Loopback, port)

let mutable loop = Unchecked.defaultof<Loop>
let mutable server = Unchecked.defaultof<IDisposable>

type ServerType =
  | Tcp
  | Pipe
  | Udp

let getPipeName() =
  if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
    "\\\\?\\pipe\\echo"
  else
   "/tmp/echo"

let onClosed =
  Action<_>(fun (handle: 't when 't :> IDisposable) -> handle.Dispose())

let onError =
  Action<_,_>(fun (handle: 't when 't :> IDisposable) (exn: Exception) ->
    printfn "Echo server read error %O" exn)

let onWriteCompleted =
  Action<_,_>(fun (stream: StreamHandle) (exn: Exception) ->
    if not (isNull exn) then
      printfn "Echo server write error %O" exn
      stream.CloseHandle(onClosed))

let onAccept =
  Action<_,_>(fun (stream:StreamHandle) (data:ReadableBuffer) ->
    let message = data.ReadString(Encoding.UTF8)
    if not (String.IsNullOrEmpty(message)) then
      printfn "Echo server received : %s" message
      //
      // Scan for the letter Q which signals that we should quit the server.
      // If we get QS it means close the stream.
      //
      if (message.StartsWith("Q")) then
        printfn "Echo server closing stream."
        stream.Dispose()

        if (message.EndsWith("QS")) then
          printfn "Echo server shutting down."
          server.Dispose()
      else
        printfn "Echo server sending echo back."
        let array = Encoding.UTF8.GetBytes(sprintf "ECHO [%s]" message)
        let buffer = WritableBuffer.From(array)
        stream.QueueWriteStream(buffer, onWriteCompleted))

let onConnection =
  Action<_,_>( fun (client: 't when 't :> StreamHandle) (exn: Exception) ->
    if not (isNull exn) then
      printfn "%s:Echo server client connection failed %O" (typeof<'t>.Name) exn
      client.CloseHandle(onClosed)
    else
      printfn "%s:Echo server client connection accepted" (typeof<'t>.Name)
      client.OnRead(onAccept, onError))

let onSendCompleted =
  Action<_,_>(fun (udp: Udp) (exn: Exception) ->
    if not (isNull exn) then
      printfn "Echo server send error %O" exn
    udp.CloseHandle(onClosed))

let onReceive =
  Action<_,_>(fun (udp: Udp) (completion: IDatagramReadCompletion) ->
    if not (isNull completion.Error) then
      printfn "Echo server receive error %O" completion.Error
      udp.CloseHandle(onClosed)
    else
      let remoteEndPoint = completion.RemoteEndPoint
      let data = completion.Data
      let message = data.ReadString(Encoding.UTF8)
      if not (String.IsNullOrEmpty(message)) then
        printfn "Echo server received : %s from %O" message remoteEndPoint
        printfn "Echo server sending echo back to %O." remoteEndPoint
        let array = Encoding.UTF8.GetBytes(sprintf "ECHO [%s]" message)
        let buffer = WritableBuffer.From(array)
        udp.QueueSend(buffer, remoteEndPoint, onSendCompleted))

let startServer serverType =
  loop <- new Loop()
  match serverType with
  | Udp ->
    let endPoint = IPEndPoint(IPAddress.Any, port)
    server <- loop.CreateUdp().Bind(endPoint).MulticastLoopback(true).ReceiveStart(onReceive)
    printfn "%O:Echo server receive started." serverType
  | Pipe ->
    let name = getPipeName()
    server <- loop.CreatePipe().Listen(name, onConnection)
    printfn "%O:Echo server started on %s." serverType name
  | Tcp ->
    server <- loop.CreateTcp().SimultaneousAccepts(true).Listen(endPoint, onConnection)
    printfn "%O:Echo server started on %O." serverType endPoint
  loop.RunDefault() |> printfn "result: %d"
  printfn "%O:Echo server loop completed." serverType

[<EntryPoint>]
let main argv =
  LogFactory.AddConsoleProvider();

  try
    startServer Tcp
  with
    | exn ->
      printfn "Echo server error %O." exn

  Console.ReadLine() |> ignore
  loop.Dispose()

  0 // return an integer exit code
