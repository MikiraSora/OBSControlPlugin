using OBSWebsocketDotNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OBSControl
{
    public class CommandParser
    {
        private readonly OBSWebsocket obs;

        public CommandParser(OBSWebsocket obs)
        {
            this.obs = obs;
        }

        /*
         * Label444 show
         * 场景2.Label set translate=45,20
         * 场景2.Label set property positionX=45 positionY=200
         * 场景2 show
         */
        public void ParseCommand(ControlItem item)
        {
            Action action = ()=> { };

            foreach (var command_line in item.Commands)
            {
                if (string.IsNullOrWhiteSpace(command_line))
                    continue;
                
                var data = command_line.Split(' ');

                var control_handler = ParseName(data[0]);

                if (data.Length > 1)
                {
                    var exec = data[1];

                    switch (exec.ToLower())
                    {
                        case "show":
                            action += CommandShow(control_handler);
                            break;
                        case "hide":
                            action += CommandHide(control_handler);
                            break;
                        case "set":
                            action += CommandSet(control_handler, data);
                            break;
                        default:
                            break;
                    }
                }
                else
                    throw new Exception("Invaild format command.");
            }

            item.CachedExecutableAction = action;
        }

        private Action CommandSet((string scene_name, string control_name) control_handler, string[] data)
        {
            //name set variable position
            if (data.Length<4)
                throw new Exception("Invaild format command.");

            switch (data[2].ToLower())
            {
                case "property":
                    return CommandSetProperty(control_handler, data);
                case "position":
                case "location":
                    var d = data[3].Split(',');
                    if (d.Length<2)
                        throw new Exception("Invaild format command.");
                    return CommandSetPosition(control_handler,(float.Parse(d[0]), float.Parse(d[1])));
                default:
                    throw new Exception("Unknown variable to set");
            }
        }


        static FieldInfo[] ITEM_FIELD_INFOS = typeof(SceneItemProperties).GetFields();

        private Action CommandSetProperty((string scene_name, string control_name) control_handler, string[] data)
        {
            SceneItemProperties properties = new SceneItemProperties();

            foreach (var data_set in data.Skip(3))
            {
                var s = data_set.Split('=');

                if (s.Length < 2)
                    throw new Exception("Invaild format command.");

                var variable = s[0].ToLower();
                var value_str = s[1];

                if (!(ITEM_FIELD_INFOS.FirstOrDefault(x=>x.Name.ToLower()== variable) is FieldInfo fi))
                    throw new Exception($"Invaild variable name {s[0]}.");

                try
                {
                    var converter = TypeDescriptor.GetConverter(fi.FieldType);
                    var value = converter.ConvertFromString(value_str);
                    fi.SetValue(properties, value);
                }
                catch (Exception e)
                {
                    throw new Exception($"Invaild variable {s[0]} value {value_str} ({e.Message}).");
                }
            }

            return () => obs.SetSceneItemProperties(control_handler.control_name, properties, control_handler.scene_name);
        }

        private Action CommandSetPosition((string scene_name, string control_name) control_handler, (float x, float y) position)
            => () => obs.SetSceneItemPosition(control_handler.control_name, position.x, position.y, control_handler.scene_name);

        private Action CommandShow((string scene_name, string control_name) control_handler)
            => string.IsNullOrWhiteSpace(control_handler.control_name) ? 
            (Action)(
            () => obs.SetCurrentScene(control_handler.scene_name)
            ) 
            : 
            () => obs.SetSourceRender(control_handler.control_name, true, control_handler.scene_name);

        private Action CommandHide((string scene_name, string control_name) control_handler)
            => () => obs.SetSourceRender(control_handler.control_name, false, control_handler.scene_name);

        private (string scene_name,string control_name) ParseName(string v)
        {
            var s = v.Split('@');

            return s.Length == 1 ? (null, s[0]) : (s[0], string.Join("@",s.Skip(1)));
        }
    }
}
