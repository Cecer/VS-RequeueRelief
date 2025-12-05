using System;
using System.Linq;
using System.Reflection;
using RequeueRelief.CommandAPI;
using Vintagestory.API.Common;

namespace RequeueRelief.Commands.Settings;

internal class SetCommand : Command
{
    protected sealed override string? RequiredPrivilege { get; init; }

    protected sealed override Command[] SubCommands { get; init; }

    public SetCommand(Config config, RequeueReliefHandler queueHandler, ICoreAPI api) : base("set")
    {
        RequiredPrivilege = config.Privileges.Configure;
        SubCommands = typeof(Config.TimingsConfig).GetFields()
            .Select(field => FieldToSetting(field, config, api))
            .ToArray<Command>();
    }

    /// <summary>
    /// Generates a subcommand from a field using reflection.
    /// </summary>
    private Setting FieldToSetting(FieldInfo field, Config config, ICoreAPI api)
    {
        var name = field.Name;
        var description = field.GetCustomAttribute<ConfigDescriptionAttribute>()?.Value ?? "(No description available)";
        var units = field.GetCustomAttribute<ConfigUnitsAttribute>() ?? new ConfigUnitsAttribute(string.Empty, string.Empty);

        if (field.FieldType == typeof(double))
        {
            var validRange = field.GetCustomAttribute<ConfigValidRangeAttribute>() ?? new ConfigValidRangeAttribute(double.MinValue, double.MaxValue);
            return new DoubleSetting(name, description, validRange, () => (double) field.GetValue(config)!, value =>
            {
                field.SetValue(config.Timings, value);
                Config.Save(api, config);
            }, units);
        }

        throw new NotImplementedException($"Unsupported setting type: {field.FieldType}");
    }

    private abstract class Setting(string name, string description) : Command(name)
    {
        protected override string? Description { get; init; } = description;
    }

    private class DoubleSetting(string name, string description, ConfigValidRangeAttribute validRange, Func<double> valueProvider, Action<double> valueConsumer, ConfigUnitsAttribute units) : Setting(name, description)
    {
        protected override ICommandArgumentParser[] Args { get; init; } =
        [
            new DoubleArgParser(units.Plural, validRange.MinDouble, validRange.MaxDouble, true)
        ];

        protected override TextCommandResult Handle(TextCommandCallingArgs args)
        {
            var oldValue = valueProvider();
            var newValue = (double)args[0];
            valueConsumer(newValue);
            return TextCommandResult.Success($"[RequeueRelief] Changed {Name} from {oldValue} to {newValue} {units.Resolve(newValue)}.");
        }
    }
}