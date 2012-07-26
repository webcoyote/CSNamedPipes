# CSNamedPipes

CSNamedPipes is a demo application that implements interprocess communication (IPC) using Named Pipes in C#.


## Why create this? What problems does it solve?

I needed a library to implement interprocess communication so that I could write a desktop application communicate with a Windows Service application. I thought I'd find some simple code on the Internet, but two things were missing:

1. Most of the code samples I ran across used synchronous (blocking) communication, which requires one thread per named pipe. My background is writing massive-scale Internet services like battle.net, and online games like Starcraft and Guild Wars, which would totally fall over using synchronous sockets/pipes. So async it is!

2. Google for “How to detect a client disconnect using a named pipe” and you’ll get 430000 hits. I wanted to make sure my program solved this problem.

For more details about the solutions to these problems, you can read the code or check out my blog article [Detect client disconnects using named-pipes in C#](http://www.codeofhonor.com/blog/detect-client-disconnects-using-named-pipes-in-csharp).

## Comments

I am *glad* to answer questions about this project.

## License

MIT License, which basically means you can do whatever you want with the code (even use it commercially with no fee) but don't blame me if something bad happens.
