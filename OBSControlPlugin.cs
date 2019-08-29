using OsuRTDataProvider;
using Sync.Plugins;
using Sync.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OBSWebsocketDotNet;
using Newtonsoft.Json;
using OsuRTDataProvider.Listen;
using static OsuRTDataProvider.Listen.OsuListenerManager;
using System.IO;
using Sync.Tools.ConfigurationAttribute;

namespace OBSControl
{
    [SyncRequirePlugin(typeof(OsuRTDataProviderPlugin))]
    public class OBSControlPlugin : Plugin, IConfigurable
    {
        private Logger logger { get; } = new Logger<OBSControlPlugin>();
        private PluginConfigurationManager config_manager;

        public Dictionary<OsuStatus,ControlItem> StatusSwitchers = new Dictionary<OsuStatus, ControlItem>();

        private OBSWebsocket obs_remote;
        private string default_scene_name;
        private string current_scene_name;

        [Path(IsDirectory =false,RequireRestart = true)]
        public ConfigurationElement SwitchTableFilePath { get; set; } = "./status_switch_table.json";

        public ConfigurationElement OBSRemoteHostAndPort { get; set; } = "ws://localhost:4444";
        public ConfigurationElement OBSRemotePassword { get; set; } = "JXU5RUJCJXU4MkIxJXU3MjVCJXU5MDNDJXU2MjExJXU1NzgzJXU1NzNF";

        public ConfigurationElement DefaultStatus { get; set; } = OsuStatus.Idle.ToString();

        public OBSControlPlugin() : base(nameof(OBSControlPlugin), "MikiraSora")
        {
            EventBus.BindEvent<PluginEvents.LoadCompleteEvent>(OnLoaded);

            config_manager = new PluginConfigurationManager(this);
            config_manager.AddItem(this);
        }

        private void OnLoaded(PluginEvents.LoadCompleteEvent @event)
        {
            if (!LoadConfig())
            {
                logger.LogError($"Load config files failed.");
                return;
            }

            InitORTDP(@event);
            InitOBSRemote();

            logger.LogInfomation("Init done.");
        }

        private void InitOBSRemote()
        {
            try
            {
                logger.LogInfomation($"Initialize OBS websocket ....");

                obs_remote = new OBSWebsocket();

                var host_port = (string)OBSRemoteHostAndPort;
                host_port = host_port.StartsWith("ws://") ? host_port : ("ws://" + host_port);

                obs_remote.Connected += OnOBSConnected;
                obs_remote.Disconnected += OnOBSDisconnected;

                logger.LogInfomation($"OBS websocket is connecting....");
                obs_remote.Connect(host_port, OBSRemotePassword);

                logger.LogInfomation($"Initialize OBS websocket successfully.");

                BuildCommandActions();
                SetupDefaultScene();
            }
            catch (Exception e)
            {
                logger.LogError($"Initialize OBS websocket failed! "+e.Message);
                obs_remote = null;
                return;
            }
        }

        private void BuildCommandActions()
        {
            var parser = new CommandParser(obs_remote);

            foreach (var item in StatusSwitchers)
            {
                parser.ParseCommand(item.Value);
            }
        }

        private void SetupDefaultScene()
        {
            if (!StatusSwitchers.Keys.Any(x=>x.ToString()==DefaultStatus))
            {
                logger.LogError($"option DefaultStatus is \"{DefaultStatus}\" but your config doesn't define its commands." +
                    $"default commands will be the first of status you define named \"{obs_remote.ListScenes().Select(x => x.Name).FirstOrDefault()??string.Empty}\".");
                default_scene_name = StatusSwitchers.Keys.FirstOrDefault().ToString();
            }
            else
            {
                default_scene_name = DefaultStatus;
            }

            ExecuteCommands(StatusSwitchers[Enum.TryParse(default_scene_name, out OsuStatus v) ? v : OsuStatus.Idle]);
        }

        private void OnOBSDisconnected(object sender, EventArgs e)
        {
            logger.LogInfomation($"OBS websocket is disconnected.");
        }

        private void OnOBSConnected(object sender, EventArgs e)
        {
            logger.LogInfomation($"OBS websocket is connect.");
        }

        private bool LoadConfig()
        {
            try
            {
                if (!File.Exists(SwitchTableFilePath))
                {
                    logger.LogInfomation("switch table file not found , try to create default.");
                    StatusSwitchers = new Dictionary<OsuStatus, ControlItem>();
                    StatusSwitchers.Add(OsuStatus.Idle, new ControlItem());
                    File.WriteAllText(SwitchTableFilePath, JsonConvert.SerializeObject(StatusSwitchers));
                }

                StatusSwitchers = JsonConvert.DeserializeObject<Dictionary<OsuStatus,ControlItem>>(File.ReadAllText(SwitchTableFilePath));
                return true;
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
                return false;
            }
        }

        private void InitORTDP(PluginEvents.LoadCompleteEvent @event)
        {
            var ortdp_plugin = @event.Host.EnumPluings().OfType<OsuRTDataProviderPlugin>().FirstOrDefault();

            if (ortdp_plugin == null)
            {
                logger.LogError("Can't find ortdp plugin.");
                return;
            }

            ortdp_plugin.ListenerManager.OnStatusChanged += (prev,now) => {
                if (prev == now)
                    return;
                OnStatusChanged(now);
            };

            logger.LogInfomation("Initialize ORTDP event successfully.");
        }

        private void OnStatusChanged(OsuStatus now)
        {
            logger.LogInfomation($"Now osu status is {now.ToString()}");

            if (StatusSwitchers.TryGetValue(now, out var command))
                ExecuteCommands(command);
            else if (Enum.TryParse(default_scene_name, out now))
                if (StatusSwitchers.TryGetValue(now, out command))
                    ExecuteCommands(command);
        }

        private void ExecuteCommands(ControlItem command)
        {
            command.CachedExecutableAction?.Invoke();
        }

        private void ChangeScene(string scene_name)
        {
            if (string.IsNullOrWhiteSpace(scene_name))
            {
                logger.LogError($"Param scene_name is null/empty.");
                return;
            }

            var scenes = obs_remote.ListScenes();

            if (scenes.Count==0)
            {
                logger.LogError($"There is no any scene avaliable in OBS.");
                return;
            }

            if (!obs_remote.ListScenes().Select(x=>x.Name).Any(x=>x==scene_name))
            {
                logger.LogError($"OBS scene { scene_name} not found.");
                return;
            }

            if (current_scene_name == scene_name)
                return;

            current_scene_name = scene_name;

            logger.LogInfomation($"Try to switch scene to { scene_name}.");
            obs_remote.SetCurrentScene(scene_name);
            logger.LogInfomation($"OBS scene is switched to {scene_name}.");
        }

        public void onConfigurationLoad()
        {

        }

        public void onConfigurationReload()
        {

        }

        public void onConfigurationSave()
        {

        }


    }
}
