﻿using System.CommandLine;
using System.IO.Pipes;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using FSharp.Compiler.Syntax;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Connection;
using Microsoft.DotNet.Interactive.CSharp;
using WpfConnect;

namespace JupyterCAD;


public class App : IExtensionApplication
{
    private CompositeKernel _kernel;

    private const string NamedPipeName = "InteractiveCad";

    private bool RunOnDispatcher { get; set; }
    public void Initialize()
    {
        
        // write a message to the command line
        Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nHello World!\n");
        _kernel = new CompositeKernel();

        SetUpNamedPipeKernelConnection();

        AddDispatcherMagicCommand(_kernel);

        CSharpKernel csharpKernel = RegisterCSharpKernel();

        var _ = Task.Run(async () =>
        {
            //Load WPF app assembly 
            string ass = typeof(SynExpr.App).Assembly.Location;
            await csharpKernel.SendAsync(new SubmitCode(@$"#r ""{ass}""
using {nameof(WpfConnect)};"));
            //Add the WPF app as a variable that can be accessed
            await csharpKernel.SetValueAsync("App", this, GetType());

            //Start named pipe
            _kernel.AddKernelConnector(new ConnectNamedPipeCommand());
        });
    }
    private void SetUpNamedPipeKernelConnection()
    {
        var serverStream = new NamedPipeServerStream(
            NamedPipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous);

        var sender = KernelCommandAndEventSender.FromNamedPipe(
            serverStream,
            new Uri("kernel://remote-control"));

        var receiver = KernelCommandAndEventReceiver.FromNamedPipe(serverStream);

        var host = _kernel.UseHost(sender, receiver, new Uri("kernel://my-wpf-app"));

        _kernel.RegisterForDisposal(host);
        _kernel.RegisterForDisposal(receiver);
        _kernel.RegisterForDisposal(serverStream);

        var _ = Task.Run(() =>
        {
            // required as waiting connection on named pipe server will block
            serverStream.WaitForConnection();
            var _ = host.ConnectAsync();
        });
    }

    public void Terminate()
    {
        throw new NotImplementedException();
    }
    private void AddDispatcherMagicCommand(Kernel kernel)
    {
        var enabledOption = new Option<bool>("--enabled", getDefaultValue: () => true);
        var dispatcherCommand = new Command("#!dispatcher", "Enable or disable running code on the Dispatcher")
        {
            enabledOption
        };

        dispatcherCommand.SetHandler(
            enabled => RunOnDispatcher = enabled,
            enabledOption);

        kernel.AddDirective(dispatcherCommand);
    }

    private CSharpKernel RegisterCSharpKernel()
    {
        var csharpKernel = new CSharpKernel()
            .UseNugetDirective()
            .UseKernelHelpers()
            .UseWho()
            .UseValueSharing()
            //This is added locally
            .UseWpf();

        _kernel.Add(csharpKernel);

        csharpKernel.AddMiddleware(async (KernelCommand command, KernelInvocationContext context, KernelPipelineContinuation next) =>
        {
            if (RunOnDispatcher)
            {
                await Dispatcher.CurrentDispatcher.InvokeAsync(async () => await next(command, context));
            }
            else
            {
                await next(command, context);
            }
        });

        return csharpKernel;
    }

}