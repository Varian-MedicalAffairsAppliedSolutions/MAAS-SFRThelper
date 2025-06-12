﻿//using System.Windows.Threading;
//using System;
//using VMS.TPS.Common.Model.API;

//public class EsapiWorker
//{
//    private readonly ScriptContext _scriptContext;
//    private readonly Dispatcher _dispatcher;

//    public EsapiWorker(ScriptContext scriptContext)
//    {
//        _scriptContext = scriptContext;
//        _dispatcher = Dispatcher.CurrentDispatcher;
//    }

//    public void Run(Action<ScriptContext> a)
//    {
//        _dispatcher.BeginInvoke(a, _scriptContext);
//    }
//    public void RunWithWait(Action<ScriptContext> a)
//    {
//        _dispatcher.BeginInvoke(a, _scriptContext).Wait();
//    }
//}

using System;
using System.Diagnostics.Eventing.Reader;
using System.Windows.Threading;
using VMS.TPS.Common.Model.API;

public class EsapiWorker
{
    private readonly ScriptContext _scriptContext;
    private readonly Dispatcher _dispatcher;

    // Add this public property so other code can get the ScriptContext
    // public ScriptContext Context => _scriptContext;

    public EsapiWorker(ScriptContext scriptContext)
    {
        _scriptContext = scriptContext;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public void Run(Action<ScriptContext> a)
    {
        _dispatcher.BeginInvoke(a, _scriptContext);
    }

    public void RunWithWait(Action<ScriptContext> a)
    {
        _dispatcher.BeginInvoke(a, _scriptContext).Wait();
    }

    
}
