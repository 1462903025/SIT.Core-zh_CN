﻿using Newtonsoft.Json;
using SIT.Tarkov.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SIT.Core.Coop
{
    public abstract class ModuleReplicationPatch : ModulePatch, IModuleReplicationPatch
    {
        public static List<ModuleReplicationPatch> Patches { get; } = new List<ModuleReplicationPatch>();

        public ModuleReplicationPatch()
        {
            if (Patches.Any(x => x.GetType() == this.GetType()))
            {
                Logger.LogError($"Attempted to recreate {this.GetType()} Patch");
                return;
            }

            Patches.Add(this);
            LastSent.TryAdd(GetType(), new Dictionary<string, object>());
        }

        public abstract Type InstanceType { get; }
        public abstract string MethodName { get; }

        public virtual bool DisablePatch { get; } = false;

        protected static ConcurrentDictionary<Type, Dictionary<string, object>> LastSent = new();


        public static string SerializeObject(object o)
        {
            try
            {
                return o.SITToJson();
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
            }
            return string.Empty;
        }

        public static T DeserializeObject<T>(string s)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(s, PatchConstants.GetJsonSerializerSettings());
            }
            catch (Exception e)
            {
                Logger.LogError(e.ToString());
            }
            return default(T);
        }

        public abstract void Replicated(EFT.Player player, Dictionary<string, object> dict);

        protected static ConcurrentDictionary<Type, ConcurrentDictionary<string, ConcurrentBag<long>>> ProcessedCalls = new();

        protected static bool HasProcessed(Type type, EFT.Player player, Dictionary<string, object> dict)
        {
            if (!ProcessedCalls.ContainsKey(type))
                ProcessedCalls.TryAdd(type, new ConcurrentDictionary<string, ConcurrentBag<long>>());

            var playerId = player.Id.ToString();
            var timestamp = long.Parse(dict["t"].ToString());
            if (!ProcessedCalls[type].ContainsKey(playerId))
            {
                Logger.LogDebug($"Adding {playerId},{timestamp} to {type} Processed Calls Dictionary");
                ProcessedCalls[type].TryAdd(playerId, new ConcurrentBag<long>());
                //ProcessedCalls[type][playerId].Add(timestamp);
            }

            if (!ProcessedCalls[type][playerId].Contains(timestamp))
            {
                ProcessedCalls[type][playerId].Add(timestamp);
                return false;
            }

            return true;
        }

        public static void Replicate(Type type, EFT.Player player, Dictionary<string, object> dict)
        {
            if (!Patches.Any(x => x.GetType().Equals(type)))
                return;

            var p = Patches.Single(x => x.GetType().Equals(type));
            p.Replicated(player, dict);
        }
    }
}
