# SoopChatNet
 Sooplive(KR) Chat API Lightweight C# Wrapper of [wakscord/afreeca](https://github.com/wakscord/afreeca)

# Requirements
 - .NET Standard 2.1

# Installation
 1. Download UnityPackage from the [Releases](https://github.com/w0lfv/releases).
 2. Import the Unitypackage into your Unity project.

# API
## SoopChat class
 - **Constructor**
   - `SoopChat(string streamerId, string password = null, int maxQueueSize = 500)`: Create chat connection instance to specified streamer ID.
 - **Events**
   - `OnMessageReceived`: Triggered when a new chat message is received. `Chat` instance will be given.
   - `OnSocketOpened`: Triggered when the socket connection is successfully opened.
   - `OnSocketError`: Triggered when there is an error with the socket connection.
   - `OnSocketClosed`: Triggered when the socket connection closes.
 - **Methods**
   - `OpenAsync()`: Opens the WebSocket connection asynchronously.
   - `CloseAsync()`: Closes the WebSocket connection asynchronously.
   - `Update()`: Polls for new messages and handles socket operations.
   - `Dispose()`: Releases all resources and cleans up the connection.
## Chat class
 The Chat class represents an individual chat message with various metadata properties:
 - **Events**
   - `string sender`: The unique identifier of the message sender.
   - `string nickname`: The display name of the sender.
   - `string message`: The actual chat message content.
   - `int lang`:
   - `int subMonth`: Indicates how many months a user has been a subscriber.
   - `UserStatusFlag1 flag1`: The first user status flag.
   - `UserStatusFlag2 flag2`: The second user status flag.
 See [Example.cs](./Assets/Scripts/Example.cs) for Unity example.

# TODO
 - - [ ] Optimization
   - - [ ] Object pooling

# License
 This project is licensed under the MIT License. See the [LICENSE](./LICENSE) file for more information.