using System;
using System.Collections.Generic;
using ECController.Driver;
using ECController.Models;
using ECController.Services;

namespace ECController.Services
{
    /// <summary>单个地址的读写结果。</summary>
    public class EcItemResult
    {
        public byte Address { get; set; }

        /// <summary>期望写入的值。</summary>
        public byte Target { get; set; }

        /// <summary>回读得到的实际值；未读取时无意义。</summary>
        public byte Actual { get; set; }

        /// <summary>是否已执行过回读。</summary>
        public bool HasActual { get; set; }

        /// <summary>回读是否与目标一致。</summary>
        public bool Matches
        {
            get { return HasActual && Actual == Target; }
        }

        /// <summary>该地址写入/读取过程中的错误信息，null 表示无错。</summary>
        public string Error { get; set; }
    }

    /// <summary>一次"应用模式"的完整结果。</summary>
    public class ApplyResult
    {
        public List<EcItemResult> Items { get; set; }

        /// <summary>整体是否成功（所有地址都写入且回读一致）。</summary>
        public bool Success { get; set; }

        public string Message { get; set; }
    }

    /// <summary>
    /// EC 读写业务层。把"遍历模式 -> 写入 -> 回读 -> 校验"的流程
    /// 从界面代码里抽出来，供 GUI 和开机自动应用共用。
    /// </summary>
    public sealed class EcService : IDisposable
    {
        private readonly EcAccess _ec = new EcAccess();

        /// <summary>驱动是否已初始化并且设备可打开。</summary>
        public bool IsReady
        {
            get { return _ec.IsOpen; }
        }

        /// <summary>
        /// 写入全部结束后、开始回读校验前的等待毫秒数。
        /// 给 EC 固件时间把新值反映到读回路径，避免误报不一致。
        /// </summary>
        private const int WriteSettleDelayMs = 200;

        /// <summary>
        /// 判定一个读数"可信"所需的连续相同次数。
        ///
        /// 本机 EC 的端口 0x62/0x66 是与系统/厂商 EC 驱动共享的全局资源，
        /// 我们走 WinRing0 直接读端口，会和它们抢总线。实测一次写入后连续
        /// 读 4 次可能得到 [错, 对, 对, 错]，因此单次读数**不可信**，
        /// 必须连续两次读到同一个值才认可。
        /// </summary>
        private const int StableReadCount = 2;

        /// <summary>取可信读数时最多尝试几次。</summary>
        private const int StableReadAttempts = 10;

        /// <summary>校验重试之间的间隔毫秒数。</summary>
        private const int VerifyRetryDelayMs = 30;

        /// <summary>每次端口操作后的等待毫秒数。</summary>
        public int WaitTime { get; set; }

        public EcService(int waitTime)
        {
            WaitTime = waitTime > 0 ? waitTime : 5;
        }

        /// <summary>
        /// 初始化驱动与 EC 设备。失败抛 <see cref="DriverLoadException"/>
        /// 或 <see cref="EcAccessException"/>。
        /// </summary>
        public void Initialize()
        {
            DriverLoader.InitializeDriver();
            _ec.Init();
        }

        /// <summary>读取单个寄存器。</summary>
        public byte Read(byte address)
        {
            return _ec.ReadEC(address, WaitTime);
        }

        /// <summary>写入单个寄存器。</summary>
        public void Write(byte address, byte value)
        {
            _ec.WriteEC(address, value, WaitTime);
        }

        /// <summary>
        /// 用于校验的读取：取一个可信读数（连续两次一致才认可）。
        ///
        /// 实测本机 EC 的 0x78/0x79 写入后，连续读 4 次可能得到
        /// [错, 对, 对, 错]——端口被系统/厂商 EC 驱动共享导致的串扰。
        /// 因此这里不能只读一次，也不能"读到目标值就算过"。
        /// </summary>
        public byte ReadForVerify(byte address)
        {
            bool hasValue;
            return ReadStable(address, out hasValue);
        }

        /// <summary>
        /// 读取一组地址的当前值。单项失败不会中断整体，
        /// 失败信息记录在对应结果的 <see cref="EcItemResult.Error"/> 上。
        /// </summary>
        public List<EcItemResult> ReadAll(IEnumerable<EcItem> items)
        {
            List<EcItemResult> results = new List<EcItemResult>();

            foreach (EcItem item in items)
            {
                EcItemResult result = new EcItemResult
                {
                    Address = item.Address,
                    Target = item.Value
                };

                try
                {
                    bool hasValue;
                    result.Actual = ReadStable(item.Address, out hasValue);
                    result.HasActual = hasValue;
                }
                catch (Exception ex)
                {
                    result.Error = ex.Message;
                    Logger.Error(
                        string.Format("读取 EC 0x{0:X2} 失败。", item.Address),
                        ex);
                }

                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// 应用一个模式：逐个写入，全部写完后统一回读校验。
        /// 任何一步抛出异常都会被捕获并记录，结果体现在返回值中。
        /// </summary>
        public ApplyResult Apply(EcMode mode)
        {
            ApplyResult result = new ApplyResult
            {
                Items = new List<EcItemResult>(),
                Success = false
            };

            if (mode == null)
            {
                result.Message = "未指定要应用的模式。";
                return result;
            }

            Logger.Info("开始应用模式：" + mode.Name);

            // 第一阶段：写入
            foreach (EcItem item in mode.Items)
            {
                EcItemResult itemResult = new EcItemResult
                {
                    Address = item.Address,
                    Target = item.Value
                };

                try
                {
                    _ec.WriteEC(item.Address, item.Value, WaitTime);
                }
                catch (Exception ex)
                {
                    itemResult.Error = ex.Message;
                    Logger.Error(
                        string.Format(
                            "写入 EC 0x{0:X2} = 0x{1:X2} 失败。",
                            item.Address,
                            item.Value),
                        ex);
                }

                result.Items.Add(itemResult);
            }

            // 写入全部完成后稍等片刻再回读。
            // 实测：EC 固件需要一点时间把新值反映到读回路径上，
            // 刚写完立刻读可能拿到旧值，导致误判为不一致。
            System.Threading.Thread.Sleep(WriteSettleDelayMs);

            // 第二阶段：回读校验（只校验写入未报错的项）
            int mismatch = 0;
            int failed = 0;

            foreach (EcItemResult itemResult in result.Items)
            {
                if (itemResult.Error != null)
                {
                    failed++;
                    continue;
                }

                try
                {
                    bool hasValue;
                    byte actual = ReadStable(itemResult.Address, out hasValue);

                    itemResult.Actual = actual;
                    itemResult.HasActual = hasValue;

                    if (!itemResult.Matches)
                    {
                        mismatch++;

                        Logger.Warn(string.Format(
                            "校验不一致：0x{0:X2} 目标 0x{1:X2}，实际 0x{2:X2}。",
                            itemResult.Address,
                            itemResult.Target,
                            itemResult.Actual));
                    }
                }
                catch (Exception ex)
                {
                    itemResult.Error = ex.Message;
                    failed++;

                    Logger.Error(
                        string.Format("回读 EC 0x{0:X2} 失败。", itemResult.Address),
                        ex);
                }
            }

            result.Success = failed == 0 && mismatch == 0;

            if (result.Success)
            {
                result.Message = string.Format(
                    "已应用 {0}，{1} 个地址全部校验通过。",
                    mode.Name,
                    result.Items.Count);

                Logger.Info(result.Message);
            }
            else
            {
                result.Message = string.Format(
                    "应用 {0} 未完全成功：{1} 个地址校验不一致，{2} 个地址读写失败。",
                    mode.Name,
                    mismatch,
                    failed);

                Logger.Warn(result.Message);
            }

            return result;
        }

        /// <summary>
        /// 取一个可信读数：连续 <see cref="StableReadCount"/> 次读到同一个值才认可。
        ///
        /// 为什么不"读到目标值就算成功"：那样等于用期望值去筛读数，
        /// 一旦某次被串扰的读数碰巧等于目标值就会把失败判成成功，是为不诚实。
        /// 这里只判断"读到的值是否稳定"，再交给上层与目标值比较。
        ///
        /// <paramref name="hasValue"/> 为 false 表示始终没能取得稳定读数
        /// （EC 被其它驱动持续抢占），此时返回值不可信，应报"读取不稳定"。
        /// </summary>
        private byte ReadStable(byte address, out bool hasValue)
        {
            hasValue = false;

            byte candidate = 0;
            byte lastSeen = 0;
            int consecutive = 0;
            bool readAnything = false;

            for (int attempt = 0; attempt < StableReadAttempts; attempt++)
            {
                byte value = _ec.ReadEC(address, WaitTime);

                readAnything = true;
                lastSeen = value;

                if (consecutive > 0 && value == candidate)
                {
                    consecutive++;

                    if (consecutive >= StableReadCount)
                    {
                        hasValue = true;
                        return candidate;
                    }
                }
                else
                {
                    // 与上一次不同，重新开始计数
                    candidate = value;
                    consecutive = 1;
                }

                if (attempt < StableReadAttempts - 1)
                    System.Threading.Thread.Sleep(VerifyRetryDelayMs);
            }

            return readAnything ? lastSeen : (byte)0;
        }

        /// <summary>仅做回读校验，不写入。用于"读取当前 EC"按钮。</summary>
        public List<EcItemResult> Verify(EcMode mode)
        {
            if (mode == null)
                return new List<EcItemResult>();

            return ReadAll(mode.Items);
        }

        public void Dispose()
        {
            _ec.Dispose();
        }
    }
}
