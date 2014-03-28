﻿using System;
using System.Collections.Generic;

namespace R.MessageBus.Interfaces
{
    public interface IBusContainer
    {
        IEnumerable<HandlerReference> GetHandlerTypes();
        IEnumerable<HandlerReference> GetHandlerTypes(Type messageHandler);
        object GetInstance(Type handlerType);
        T GetInstance<T>();
        void ScanForHandlers();
        void Initialize();
    }
}