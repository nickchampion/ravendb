// -----------------------------------------------------------------------
//  <copyright file="IOperationState.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Raven.Json.Linq;

namespace Raven.Abstractions.Data
{
    public class OperationStateBase : IOperationState
    {
        public bool Completed { get; private set; } 
        public bool Faulted { get; private set; }
        public bool Canceled { get; private set; }
        public string State { get; set; }
        public Exception Exception { get; set; }

        public void MarkCompleted(string state = null)
        {
            VerifyState();
            State = state;
            Completed = true;
        }

        public void MarkFaulted(string state = null, Exception exception = null)
        {
            VerifyState();
            State = state;
            Exception = exception;
            Completed = true;
            Faulted = true;
        }

        public void MarkCanceled(string state = null)
        {
            VerifyState();
            State = state;
            Completed = true;
            Canceled = true;
        }

        private void VerifyState()
        {
            if (Completed)
            {
                throw new InvalidOperationException("Operation was already marked");
            }
        }
    }
}