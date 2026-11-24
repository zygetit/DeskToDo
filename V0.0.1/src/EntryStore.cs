using System; // 导入 GUID、异常和字符串比较等基础类型
using System.Collections.Generic; // 导入有序列表和集合
using System.Diagnostics; // 导入通过 Windows Shell 启动进程的类型
using System.Globalization; // 使用固定的数字日期格式，不受 Windows 区域设置影响
using System.IO; // 导入文件、目录和路径操作类型
using System.Linq; // 导入批量删除索引的去重和排序操作
using System.Runtime.Serialization; // 导入数据契约与序列化异常
using System.Runtime.Serialization.Json; // 导入 JSON 序列化器
using System.Xml; // 根据 JSON 根节点区分旧版清单和包含删除历史的清单

namespace DeskToDo // 将列表存储类型放在应用命名空间
{ // 开始此代码块：将列表存储类型放在应用命名空间
    [DataContract] // 声明条目可按数据契约序列化
    internal sealed class Entry // 表示列表中的一个持久化条目
    { // 开始此代码块：表示列表中的一个持久化条目
        [DataMember(IsRequired = true)] public Guid Id { get; private set; } // 保存稳定的条目编号，用于定位快捷方式副本
        [DataMember(IsRequired = true)] public string Name { get; private set; } // 保存用户可修改的显示名称
        [DataMember(IsRequired = true)] public string SourcePath { get; private set; } // 记录拖入时的完整路径
        [DataMember] public bool InternetShortcut { get; private set; } // 区分互联网快捷方式和普通 Shell 快捷方式
        [DataMember(EmitDefaultValue = false)] public string UpdatedDate { get; private set; } // 保存拖入或最近打开的日期，兼容没有日期的旧记录

        public Entry(Guid id, string name, string sourcePath, bool internetShortcut, string updatedDate) // 用完整元数据构造一个条目
        { // 开始此代码块：用完整元数据构造一个条目
            Id = id; // 保存条目编号
            Name = name; // 保存界面显示名称
            SourcePath = sourcePath; // 保存拖入时的完整路径
            InternetShortcut = internetShortcut; // 保存快捷方式格式标记
            UpdatedDate = updatedDate; // 保存已格式化的日期
        } // 结束此代码块：用完整元数据构造一个条目

        public Entry WithName(string name) // 构造只更新显示名称的新记录
        { // 开始此代码块：构造只更新显示名称的新记录
            return new Entry(Id, name, SourcePath, InternetShortcut, UpdatedDate); // 保留更新时间和其余元数据
        } // 结束此代码块：构造只更新显示名称的新记录

        public Entry WithUpdatedDate(string date) // 构造只更新日期的新记录
        { // 开始创建更新后的记录
            return new Entry(Id, Name, SourcePath, InternetShortcut, date); // 保留记录编号、名称和源路径
        } // 结束创建更新后的记录

        public static string SourceName(string path) // 从来源路径提取文件名或文件夹名
        { // 开始提取名称
            string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)); // 去掉结尾的路径分隔符
            return string.IsNullOrEmpty(name) ? path : name; // 根目录没有文件名时显示原路径
        } // 结束提取名称
    } // 结束此代码块：表示列表中的一个持久化条目

    [DataContract] // 让删除历史可以和主清单一起保存
    internal sealed class DeletedEntry // 保存可还原或打开的已删除条目
    { // 定义已删除条目的结构
        [DataMember(IsRequired = true)] public Entry Entry { get; private set; } // 保留还原所需的原条目和快捷方式编号
        [DataMember(IsRequired = true)] public string DeletedDate { get; private set; } // 保存移出主清单的本地日期

        public DeletedEntry(Entry entry, string deletedDate) // 构造一条删除历史
        { // 开始初始化删除历史
            Entry = entry; // 保存原条目
            DeletedDate = deletedDate; // 保存删除日期
        } // 完成初始化删除历史
    } // 结束已删除条目定义

    [DataContract] // 把主清单和删除历史保存在同一个 JSON 对象中
    internal sealed class ListData // 表示一次提交写入的完整清单数据
    { // 定义清单数据结构
        [DataMember(IsRequired = true)] public List<Entry> Active { get; private set; } // 主清单按手工排序保存
        [DataMember(IsRequired = true)] public List<DeletedEntry> Deleted { get; private set; } // 删除历史按最近删除的位置保存

        public ListData(List<Entry> active, List<DeletedEntry> deleted) // 封装要一起提交的两个列表
        { // 开始初始化清单数据
            Active = active; // 关联主清单
            Deleted = deleted; // 关联删除历史
        } // 完成初始化清单数据
    } // 结束清单数据定义

    internal sealed class EntryStore : IDisposable // 管理条目数据、快捷方式以及独占存储锁
    { // 开始此代码块：管理条目数据、快捷方式以及独占存储锁
        private const int ActiveLimit = 100; // 主清单最多接收一百条记录
        private const int DeletedLimit = 30; // 最多保留三十条最近删除记录
        internal const int DeleteBatchLimit = 30; // 每次最多删除三十条记录
        internal const string DeleteBatchLimitMessage = "一次性删除条数过多！"; // 统一两种删除操作的上限提示
        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(ListData)); // 读写主清单和删除历史
        private static readonly DataContractJsonSerializer LegacySerializer = new DataContractJsonSerializer(typeof(List<Entry>)); // 读取旧版主清单数组

        private readonly FileStream lease; // 在存储实例存活期间持有独占文件锁
        private readonly string dataPath; // 保存列表 JSON 的完整路径
        private readonly string shortcutDirectory; // 保存应用专用的快捷方式目录
        private List<Entry> entries; // 在内存中保存当前已提交的列表顺序
        private List<DeletedEntry> deleted; // 在内存中保存当前已提交的删除历史

        public EntryStore(string directory) // 初始化应用数据目录和列表存储
        { // 开始此代码块：初始化应用数据目录和列表存储
            dataPath = Path.Combine(directory, "entries.json"); // 计算 JSON 数据文件的路径
            shortcutDirectory = Path.Combine(directory, "Shortcuts"); // 计算快捷方式副本目录的路径
            Directory.CreateDirectory(shortcutDirectory); // 创建快捷方式目录和缺失的上级目录
            try // 尝试独占打开数据目录的锁文件
            { // 开始此代码块：尝试独占打开数据目录的锁文件
                lease = new FileStream(Path.Combine(directory, "desk.todo.lock"), // 打开用于协调并发实例的锁文件
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); // 允许首次创建锁文件，并禁止其他进程共享打开
            } // 结束此代码块：尝试独占打开数据目录的锁文件
            catch (IOException error) // 将打开锁文件时的 I/O 错误转换为应用提示
            { // 开始此代码块：将打开锁文件时的 I/O 错误转换为应用提示
                throw new IOException("无法锁定数据目录，请检查是否已有窗口在运行。", error); // 保留底层异常并提示检查是否已有实例
            } // 结束此代码块：将打开锁文件时的 I/O 错误转换为应用提示

            try // 获得锁后尝试读取已保存的列表
            { // 开始此代码块：获得锁后尝试读取已保存的列表
                ListData loaded = Load(); // 加载旧版或新版的已保存清单
                entries = loaded.Active; // 恢复主清单及其顺序
                deleted = loaded.Deleted; // 恢复最近删除记录
                if (deleted.Count > DeletedLimit) // 检查历史是否来自容量较大的旧版
                    SaveWithHistoryLimit(entries, new List<DeletedEntry>(deleted)); // 保存迁移结果并清理超出的历史副本
            } // 结束此代码块：获得锁后尝试读取已保存的列表
            catch // 处理列表读取或解析失败
            { // 开始此代码块：处理列表读取或解析失败
                lease.Dispose(); // 释放已取得的文件锁
                throw; // 将读取失败交给启动代码提示
            } // 结束此代码块：处理列表读取或解析失败
        } // 结束此代码块：初始化应用数据目录和列表存储

        public IReadOnlyList<Entry> Entries { get { return entries; } } // 以只读列表接口供界面读取已提交条目
        public IReadOnlyList<DeletedEntry> Deleted { get { return deleted; } } // 以只读接口提供最近删除记录

        public int Import(string[] paths, List<string> failures, out bool limitReached) // 导入文件并报告是否因空间不足拒绝整次拖入
        { // 开始此代码块：批量导入文件系统路径并返回成功项数
            int available = Math.Max(0, ActiveLimit - entries.Count); // 计算主清单的剩余空间
            limitReached = paths.Length > available; // 本次拖入可以恰好填满剩余空间
            if (limitReached) // 检查整次拖入能否接收
                return 0; // 在读取路径或创建快捷方式之前拒绝整批

            string importDate = CurrentDate(); // 整批文件使用开始拖入时的本地日期
            var added = new List<Entry>(); // 暂存已生成快捷方式但尚未提交的条目
            foreach (string path in paths) // 逐个处理本次拖入的文件或目录
            { // 开始此代码块：逐个处理本次拖入的文件或目录
                try // 单独处理当前路径，便于局部失败时继续
                { // 开始此代码块：单独处理当前路径，便于局部失败时继续
                    added.Add(ImportOne(path, importDate)); // 导入当前路径并设置初始更新时间
                } // 结束此代码块：单独处理当前路径，便于局部失败时继续
                catch (Exception error) // 收集当前路径的导入异常
                { // 开始此代码块：收集当前路径的导入异常
                    failures.Add(path + "\n" + error.Message); // 将路径和异常原因一起交给界面展示
                } // 结束此代码块：收集当前路径的导入异常
            } // 结束此代码块：逐个处理本次拖入的文件或目录

            if (added.Count == 0) // 检查是否没有任何成功导入的项
                return 0; // 没有可提交的条目时结束批量导入

            var updated = new List<Entry>(entries); // 复制当前列表，准备一次提交整批结果
            updated.AddRange(added); // 按拖入顺序将成功条目附加到末尾
            try // 尝试持久化更新后的整批列表
            { // 开始此代码块：尝试持久化更新后的整批列表
                Save(updated); // 将新列表写入磁盘并更新内存
                return added.Count; // 返回成功持久化的条目数
            } // 结束此代码块：尝试持久化更新后的整批列表
            catch // 处理整批列表写入失败
            { // 开始此代码块：处理整批列表写入失败
                foreach (Entry entry in added) // 遍历本批已生成的快捷方式
                    TryDelete(ShortcutPath(entry)); // 清理未能写入列表的应用快捷方式
                throw; // 保留原始异常供调用方处理
            } // 结束此代码块：处理整批列表写入失败
        } // 结束此代码块：批量导入文件系统路径并返回成功项数

        private Entry ImportOne(string path, string importDate) // 为单个来源创建条目和对应快捷方式
        { // 开始此代码块：为单个来源创建条目和对应快捷方式
            string fullPath = Path.GetFullPath(path); // 将来源规范化为完整路径
            bool isDirectory = (File.GetAttributes(fullPath) & FileAttributes.Directory) != 0; // 读取文件属性以识别目录，并检查路径是否可访问
            string name = Entry.SourceName(fullPath); // 以源文件名作为新条目的默认显示名称

            string extension = Path.GetExtension(fullPath); // 读取原路径扩展名
            bool internetShortcut = !isDirectory && extension.Equals(".url", StringComparison.OrdinalIgnoreCase); // 将实际的 .url 文件识别为互联网快捷方式
            bool copyShortcut = internetShortcut || (!isDirectory && extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)); // 判断是否应复制现有 .url 或 .lnk 文件
            var entry = new Entry(Guid.NewGuid(), name, fullPath, internetShortcut, importDate); // 用独立编号和拖入日期建立条目
            string destination = ShortcutPath(entry); // 计算应用内快捷方式的目标路径
            try // 处理创建或复制快捷方式的过程
            { // 开始此代码块：处理创建或复制快捷方式的过程
                if (copyShortcut) // 检查来源是否已经是支持的快捷方式
                    File.Copy(fullPath, destination); // 将已有快捷方式按原内容复制到应用目录
                else // 对普通文件或目录生成新快捷方式
                    WindowsShortcut.Create(fullPath, destination, isDirectory); // 调用 Windows Shell 创建指向来源的新链接
                return entry; // 将生成成功的条目交给批量导入逻辑
            } // 结束此代码块：处理创建或复制快捷方式的过程
            catch // 处理创建或复制快捷方式失败
            { // 开始此代码块：处理创建或复制快捷方式失败
                TryDelete(destination); // 清理可能留下的不完整快捷方式
                throw; // 将失败原因交给批量导入逻辑
            } // 结束此代码块：处理创建或复制快捷方式失败
        } // 结束此代码块：为单个来源创建条目和对应快捷方式

        public void Rename(int index, string name) // 更新指定条目的显示名称
        { // 开始此代码块：更新指定条目的显示名称
            name = name.Trim(); // 去除名称首尾空白
            if (name.Length == 0) // 检查去除空白后是否没有名称
                throw new ArgumentException("名称不能为空。"); // 拒绝空名称，防止列表出现无标题条目
            if (entries[index].Name == name) // 检查名称是否与已保存名称相同
                return; // 名称没有变化时结束操作

            var updated = new List<Entry>(entries); // 复制当前列表以便在持久化成功后提交
            updated[index] = updated[index].WithName(name); // 用新显示名称替换原位置的条目记录
            Save(updated); // 保存修改后的显示名称
        } // 结束此代码块：更新指定条目的显示名称

        public int Move(int index, int insertionIndex) // 将条目拖到指定插入位置并返回新索引
        { // 开始此代码块：将条目拖到指定插入位置并返回新索引
            int destination = insertionIndex > index ? insertionIndex - 1 : insertionIndex; // 向下移动时补偿原位置移除造成的索引变化
            if (destination == index) // 检查条目是否最终仍处于原位置
                return index; // 无需改动时直接返回原索引

            var updated = new List<Entry>(entries); // 复制列表以便独立修改顺序
            Entry entry = updated[index]; // 先保存准备移动的条目
            updated.RemoveAt(index); // 从旧位置移除条目
            updated.Insert(destination, entry); // 把条目插入调整后的目标位置
            Save(updated); // 将新顺序持久化
            return destination; // 将新位置返回给界面以恢复选中状态
        } // 结束此代码块：将条目拖到指定插入位置并返回新索引

        public int Remove(params int[] indices) // 将选中条目移入最近删除，并返回被淘汰的历史条数
        { // 开始移动已删除条目
            var updated = new List<Entry>(entries); // 复制主清单以便原子提交
            var history = new List<DeletedEntry>(deleted); // 复制删除历史以便与主清单一起保存
            string date = CurrentDate(); // 同一批记录使用相同的删除日期
            foreach (int index in DeletionOrder(indices)) // 从下往上移除，避免后续索引偏移
            { // 开始移动一条选中的记录
                history.Insert(0, new DeletedEntry(updated[index], date)); // 保持这批记录在主清单中的相对顺序
                updated.RemoveAt(index); // 从主清单副本移出这条记录
            } // 完成移动该记录
            return SaveWithHistoryLimit(updated, history); // 保存两个列表后返回自动淘汰的条数
        } // 结束移动已删除条目

        private int SaveWithHistoryLimit(List<Entry> active, List<DeletedEntry> history) // 保存最近三十条记录并返回淘汰数量
        { // 开始提交有容量限制的删除历史
            int expiredCount = Math.Max(0, history.Count - DeletedLimit); // 计算超出容量的最旧记录数
            List<DeletedEntry> expired = history.GetRange(history.Count - expiredCount, expiredCount); // 保存将被淘汰的记录以便清理副本
            history.RemoveRange(history.Count - expiredCount, expiredCount); // 从末尾移除超出的旧记录
            Save(active, history); // 写入成功后才清理已淘汰记录的快捷方式
            foreach (DeletedEntry item in expired) // 遍历本次被淘汰的记录
                TryDelete(ShortcutPath(item.Entry)); // 清理应用目录中的快捷方式副本
            return expiredCount; // 按实际已移除的历史记录计数
        } // 结束提交有容量限制的删除历史

        public bool Restore(int index) // 还原单条删除记录并报告是否成功
        { // 开始还原条目
            if (entries.Count >= ActiveLimit) // 还原也占用主清单容量
                return false; // 主清单已满，保留原删除记录
            var updated = new List<Entry>(entries); // 复制主清单以保留原有顺序
            updated.Add(deleted[index].Entry); // 以原显示名称和更新时间追加到末尾
            RemoveDeleted(index, updated); // 提交历史移除和主清单还原，继续使用原快捷方式
            return true; // 通知界面刷新还原结果
        } // 结束还原条目

        public bool PermanentlyRemove(params int[] indices) // 移除选中的删除历史及应用保存的快捷方式
        { // 开始永久删除
            var history = new List<DeletedEntry>(deleted); // 复制历史，用于一次提交整批删除
            var removed = new List<DeletedEntry>(); // 记录保存成功后要清理的快捷方式
            foreach (int index in DeletionOrder(indices)) // 从下往上移除选中的历史记录
            { // 开始处理一条历史记录
                removed.Add(history[index]); // 保留记录引用以定位快捷方式
                history.RemoveAt(index); // 从历史副本移出该记录
            } // 完成处理该记录
            Save(entries, history); // 先提交不可还原的整批历史移除
            bool cleaned = true; // 汇总整批快捷方式的清理结果
            foreach (DeletedEntry item in removed) // 尝试清理每条已删除记录的快捷方式
                cleaned &= TryDelete(ShortcutPath(item.Entry)); // 即使个别副本清理失败，也继续处理其他副本
            return cleaned; // 向界面报告这一批是否全部清理成功
        } // 结束永久删除

        private static IEnumerable<int> DeletionOrder(int[] indices) // 统一校验删除容量并计算安全的移除顺序
        { // 开始准备删除索引
            if (indices.Length > DeleteBatchLimit) // 存储层也检查一次删除的条数
                throw new InvalidOperationException(DeleteBatchLimitMessage); // 超出上限时终止整批删除
            return indices.Distinct().OrderByDescending(index => index); // 从最大索引开始，且每行只移除一次
        } // 完成删除索引准备

        private void RemoveDeleted(int index, List<Entry> active) // 从删除历史中移除指定项并提交两个列表
        { // 开始提交删除历史的更新
            var history = new List<DeletedEntry>(deleted); // 复制尚未修改的删除历史
            history.RemoveAt(index); // 移除指定记录
            Save(active, history); // 一次写入主清单及更新后的历史
        } // 结束提交删除历史的更新

        public void Open(int index) // 打开指定条目保存在应用目录内的快捷方式
        { // 开始此代码块：打开指定条目保存在应用目录内的快捷方式
            OpenShortcut(entries[index]); // 复用系统快捷方式打开机制
        } // 结束此代码块：打开指定条目保存在应用目录内的快捷方式

        public void OpenDeleted(int index) // 从最近删除中直接打开条目
        { // 开始打开已删除条目
            OpenShortcut(deleted[index].Entry); // 只调用 Shell，不写入历史或主清单
        } // 结束打开已删除条目

        private void OpenShortcut(Entry entry) // 打开指定条目的应用快捷方式
        { // 开始交给系统打开快捷方式
            string path = ShortcutPath(entry); // 计算要交给 Windows Shell 的快捷方式路径
            using (Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })) { } // 通过系统 Shell 打开链接并释放托管进程句柄
        } // 完成打开请求

        public void RecordOpened(int index) // 在 Windows Shell 接受打开请求后记录日期
        { // 开始更新打开日期
            string date = CurrentDate(); // 读取 Windows 本地日历中的当天日期
            if (entries[index].UpdatedDate == date) // 比较新的打开日期与已有日期
                return; // 同一天再次打开时无需重写列表

            var updated = new List<Entry>(entries); // 复制列表，保留其他记录及顺序
            updated[index] = updated[index].WithUpdatedDate(date); // 只更新本次打开的条目
            Save(updated); // 将日期保存到 JSON，供重启后恢复
        } // 结束更新打开日期

        private static string CurrentDate() // 生成可直接保存和显示的本地日期
        { // 开始格式化日期
            return DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); // 使用四位年份、两位月份和两位日期
        } // 结束格式化日期

        private string ShortcutPath(Entry entry) // 从条目编号和格式推导应用快捷方式路径
        { // 开始此代码块：从条目编号和格式推导应用快捷方式路径
            string storedExtension = entry.InternetShortcut ? ".url" : ".lnk"; // 根据实际快捷方式类型选取 .url 或 .lnk
            return Path.Combine(shortcutDirectory, entry.Id.ToString("N") + storedExtension); // 以去掉连字符的 GUID 作为存储文件名
        } // 结束此代码块：从条目编号和格式推导应用快捷方式路径

        private ListData Load() // 从 JSON 读取主清单和删除历史
        { // 开始此代码块：从 JSON 读取现有列表
            if (!File.Exists(dataPath)) // 检查列表文件是否尚未创建
                return new ListData(new List<Entry>(), new List<DeletedEntry>()); // 首次运行时创建两个空列表

            using (FileStream file = File.OpenRead(dataPath)) // 以只读方式打开列表文件
            using (XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(file, XmlDictionaryReaderQuotas.Max)) // 仅读取一次文件并识别 JSON 根节点
            { // 开始此代码块：以只读方式打开列表文件
                reader.MoveToContent(); // 定位到整个 JSON 的根节点
                ListData loaded = reader.GetAttribute("type") == "array" // 旧版直接用数组保存主清单
                    ? new ListData((List<Entry>)LegacySerializer.ReadObject(reader), new List<DeletedEntry>()) // 加载旧条目并以空删除历史升级
                    : (ListData)Serializer.ReadObject(reader); // 加载新版的两个列表
                var ids = new HashSet<Guid>(); // 检查同一个条目只存在于一个列表
                if (loaded == null || loaded.Active == null || loaded.Deleted == null // 检查两个列表是否存在
                    || loaded.Active.Exists(entry => InvalidEntry(entry, ids)) // 校验主清单的条目
                    || loaded.Deleted.Exists(item => item == null || string.IsNullOrWhiteSpace(item.DeletedDate) // 校验删除日期和条目是否存在
                        || InvalidEntry(item.Entry, ids))) // 校验删除历史的元数据和编号
                    throw new SerializationException("列表数据无效。请检查 " + dataPath); // 提示列表不合法并保留数据文件供检查
                return loaded; // 返回通过校验的原有列表
            } // 结束此代码块：以只读方式打开列表文件
        } // 结束此代码块：从 JSON 读取现有列表

        private static bool InvalidEntry(Entry entry, HashSet<Guid> ids) // 检查条目是否合法并记录唯一编号
        { // 开始校验条目
            return entry == null || entry.Id == Guid.Empty || !ids.Add(entry.Id) // 拒绝空条目、空编号或重复编号
                || string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.SourcePath); // 拒绝缺少名称或源路径的条目
        } // 完成校验条目

        private void Save(List<Entry> updated, List<DeletedEntry> history = null) // 将主清单和删除历史一起写入磁盘
        { // 开始此代码块：将更新后的条目按当前顺序写入磁盘
            var data = new ListData(updated, history ?? deleted); // 仅更新主清单时沿用现有删除历史
            string temporaryPath = dataPath + ".tmp"; // 先写入同一目录中的临时文件
            try // 进入带临时文件清理的保存流程
            { // 开始此代码块：进入带临时文件清理的保存流程
                using (var file = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None)) // 独占创建或重新写入临时文件
                { // 开始此代码块：独占创建或重新写入临时文件
                    Serializer.WriteObject(file, data); // 一次序列化两个列表，避免部分提交
                    file.Flush(true); // 提交文件缓冲，尽量确保内容已写到磁盘
                } // 结束此代码块：独占创建或重新写入临时文件

                if (File.Exists(dataPath)) // 判断是否已有正式列表文件
                    File.Replace(temporaryPath, dataPath, null); // 原子替换已有正式文件
                else // 首次保存时还没有可替换的文件
                    File.Move(temporaryPath, dataPath); // 将临时文件移动为正式文件
                entries = data.Active; // 磁盘保存成功后才替换内存主清单
                deleted = data.Deleted; // 和主清单一起替换内存删除历史
            } // 结束此代码块：进入带临时文件清理的保存流程
            finally // 保存流程结束后执行临时文件清理
            { // 开始此代码块：保存流程结束后执行临时文件清理
                TryDelete(temporaryPath); // 删除可能留在原路径的临时文件
            } // 结束此代码块：保存流程结束后执行临时文件清理
        } // 结束此代码块：将更新后的条目按当前顺序写入磁盘

        private static bool TryDelete(string path) // 尝试清理应用自己创建的文件，并返回结果
        { // 开始此代码块：尝试清理应用自己创建的文件
            try // 让清理失败不影响已经提交的数据
            { // 开始此代码块：让清理失败不影响已经提交的数据
                if (!File.Exists(path)) // 检查待清理的文件是否存在
                    return true; // 文件不存在时视为已完成清理
                FileAttributes attributes = File.GetAttributes(path); // 读取待清理副本的属性
                if ((attributes & FileAttributes.ReadOnly) != 0) // 判断副本是否继承了只读属性
                    File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly); // 仅从待清理副本移除只读标记
                File.Delete(path); // 删除应用创建的副本或临时文件
                return true; // 让调用方知道已清理快捷方式
            } // 结束此代码块：让清理失败不影响已经提交的数据
            catch (IOException) { return false; } // 报告由占用等原因造成的清理 I/O 错误
            catch (UnauthorizedAccessException) { return false; } // 报告清理阶段的访问受限错误
        } // 结束此代码块：尝试清理应用自己创建的文件

        public void Dispose() // 释放存储实例占用的资源
        { // 开始此代码块：释放存储实例占用的资源
            lease.Dispose(); // 关闭独占锁文件，允许下一实例打开数据目录
        } // 结束此代码块：释放存储实例占用的资源
    } // 结束此代码块：管理条目数据、快捷方式以及独占存储锁
} // 结束此代码块：将列表存储类型放在应用命名空间
