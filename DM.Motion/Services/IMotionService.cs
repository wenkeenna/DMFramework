using DM.Motion.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Motion.Services
{
    public interface IMotionService
    {
        IReadOnlyList<AxisConfig> Axes { get; }
        IReadOnlyList<IoConfig> Inputs { get; }
        IReadOnlyList<IoConfig> Outputs { get; }

        /// <summary>位置单位与 machine.json 一致。令牌取消时立即停轴并抛 OperationCanceledException。</summary>
        Task MoveAsync(string axis, double position, bool relative, double? velocity, CancellationToken ct);
        Task HomeAsync(string axis, CancellationToken ct);
        void Stop(string axis);
        void StopAll();
        double GetPosition(string axis);

        bool GetInput(string name);
        bool GetOutput(string name);
        void SetOutput(string name, bool on);
        Task WaitInputAsync(string name, bool expected, int timeoutMs, CancellationToken ct);   // 0=一直等；超时抛 TimeoutException
    }
}
