﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dynamo.Core;
using Dynamo.Graph.Nodes;
using VMDataBridge;

namespace DiagnosticToolkit
{
    class NodeData : NotificationObject, IDisposable
    {
        #region Private Properties
        private TimeSpan? executionTime;
        private DateTime? executionStartTime;
        private StringWriter traceWriter;
        private TraceListener traceListener;
        private string traceData = string.Empty;
        #endregion

        #region Public Properties
        public NodeModel Node { get; private set; }

        public string NodeId { get => this.Node.GUID.ToString(); }

        public double CenterX { get => this.Node.CenterX; }

        public double CenterY { get => this.Node.CenterY; }

        public int InputDataSize { get; private set; }

        public int OutputDataSize { get; private set; }

        public bool HasPerformanceData { get => executionTime.HasValue; }
        
        public int ExecutionTime
        {
            get => executionTime.HasValue ? (int)executionTime.Value.TotalMilliseconds : -1;            
        }

        public IEnumerable<PerformanceData> Statistics { get { return DiagnosticToolkitWindowViewModel.NodePerformance.GetNodePerformance(Node); } }

        public IEnumerable<int> OutputPortsDataSize { get; set; } 
        public string TraceData { get { return traceData; } }
        #endregion

        #region Public Constructors
        public NodeData(NodeModel node)
        {
            Node = node;
            Node.PropertyChanged += OnNodePropertyChanged;
            Node.NodeExecuted += OnNodeExecuted;
        } 
        #endregion

        private void OnNodeExecuted(object sender, NodeExecutedEventArgs e)
        {
            var size = Count(e.Data);
            if (size > 0 && Node.IsInputNode && !executionStartTime.HasValue) //For input node start notification may not come
                executionStartTime = DateTime.Now;

            if (e.Type == NodeExecutedType.Start)
            {
                RegisterTraceListener();

                executionStartTime = DateTime.Now;
                executionTime = null;
                InputDataSize = size;
                RaisePropertyChanged("InputDataSize");
            }
            else
            {
                executionTime = DateTime.Now.Subtract(executionStartTime.Value);
                executionStartTime = null;
                OutputDataSize = size;
                OutputPortsDataSize = (e.Data as IEnumerable).Cast<object>().Select(Count);
                UnRegisterTraceListener();

                RaisePropertyChanged("OutputDataSize");
            }
            RaisePropertyChanged("IsEvaluating");
            RaisePropertyChanged("ExecutionTime");
        }

        void OnNodePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "Position")
                RaisePropertyChanged("Node");
        }

        public void Reset()
        {
            executionStartTime = null;
            executionTime = null;
            traceData = string.Empty;
        }

        public PerformanceData GetPerformanceData()
        {
            return new PerformanceData { ExecutionTime = ExecutionTime, InputSize = InputDataSize, OutputSize = OutputDataSize };
        }


        public void Dispose()
        {
            Node = null;
            Node.NodeExecuted -= OnNodeExecuted;
            UnRegisterTraceListener();
        }

        private void UnRegisterTraceListener()
        {
            if (traceListener == null)
                return;
        
            Trace.Flush();
            traceData = traceWriter.ToString();
            
            Trace.Listeners.Remove(traceListener);
            
            traceListener.Dispose();
            traceWriter.Dispose();
            
            traceListener = null;
            traceWriter = null;
        }

        private void RegisterTraceListener()
        {
            traceData = string.Empty;
            traceWriter = new StringWriter();
            traceListener = new TextWriterTraceListener(traceWriter);
            Trace.Listeners.Add(traceListener);
        }

        private int Count(object data)
        {
            var collection = data as IEnumerable;
            if (collection == null) return 1;

            var count = 0;
            foreach (var item in collection)
            {
                count += Count(item);
            }

            return count;
        }
        
    }
}