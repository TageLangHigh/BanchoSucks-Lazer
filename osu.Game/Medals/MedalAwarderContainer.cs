// Copyright (c) GooGuTeam. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Medals.Awarders;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Medals
{
    public partial class MedalAwarderContainer : Component
    {
        private const int max_failures = 5;
        private const double retry_delay = 5000;

        private static readonly Lazy<Type[]> awarder_types = new(discoverAwarderTypes);

        private readonly List<IMedalAwarder> awarders = new List<IMedalAwarder>();

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private IBindable<APIUser> localUser = null!;
        private IBindable<APIState> apiState = null!;

        private readonly Bindable<int> failureCount = new Bindable<int>(0);

        [BackgroundDependencyLoader]
        private void load()
        {
            var discoveredAwarders = new Dictionary<int, IMedalAwarder>();

            foreach (Type awarderType in awarder_types.Value)
            {
                try
                {
                    if (Activator.CreateInstance(awarderType) is not IMedalAwarder awarder)
                        continue;

                    discoveredAwarders.TryAdd(awarder.MedalId, awarder);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to instantiate medal awarder {awarderType.FullName}: {ex}", level: LogLevel.Error);
                }
            }

            awarders.AddRange(discoveredAwarders.Values.OrderBy(a => a.MedalId));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            localUser = api.LocalUser.GetBoundCopy();
            apiState = api.State.GetBoundCopy();

            localUser.BindValueChanged(_ => refreshAwarderStates());
            apiState.BindValueChanged(_ => refreshAwarderStates(), runOnceImmediately: true);
            failureCount.BindValueChanged(onFailureCountChanged);
        }

        private void refreshAwarderStates()
        {
            failureCount.Value = 0;

            if (apiState.Value == APIState.Offline || localUser.Value.OnlineID < 2)
            {
                disableAllAwarders();
                return;
            }

            HashSet<int> ownedMedals = (localUser.Value.Achievements ?? Array.Empty<APIUserAchievement>())
                .Select(a => a.ID)
                .ToHashSet();

            foreach (IMedalAwarder awarder in awarders)
                awarder.Enabled = !ownedMedals.Contains(awarder.MedalId);
        }

        private void disableAllAwarders()
        {
            foreach (IMedalAwarder awarder in awarders)
                awarder.Enabled = false;
        }

        private void onFailureCountChanged(ValueChangedEvent<int> _)
        {
            if (failureCount.Value >= max_failures)
            {
                Logger.Log("Medal unlock failed too many times, disabling", LoggingTarget.Network);
                disableAllAwarders();
            }
        }

        protected override void Update()
        {
            base.Update();

            if (awarders.Count == 0)
                return;

            foreach (IMedalAwarder awarder in awarders)
            {
                if (!awarder.Enabled)
                    continue;

                if (awarder.CheckMedalCriteria(game))
                {
                    awarder.Enabled = false;
                    requestMedalUnlock(awarder);
                }
            }
        }

        private void requestMedalUnlock(IMedalAwarder awarder)
        {
            UnlockMedalRequest request = new UnlockMedalRequest(awarder.MedalId);

            request.Success += () => failureCount.Value = 0;

            request.Failure += ex =>
            {
                if (ex is not APIException)
                {
                    failureCount.Value++;
                    Scheduler.AddDelayed(() =>
                    {
                        if (failureCount.Value < max_failures)
                            awarder.Enabled = true;
                    }, retry_delay);
                }
            };

            api.Queue(request);
        }

        private static Type[] discoverAwarderTypes()
        {
            return getAwarderAssemblies()
                .SelectMany(getLoadableTypes)
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .Where(t => typeof(IMedalAwarder).IsAssignableFrom(t))
                .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
                .DistinctBy(t => t.FullName)
                .ToArray();
        }

        private static IEnumerable<Assembly> getAwarderAssemblies()
        {
            Assembly? assembly = tryLoadAssembly(typeof(IMedalAwarder).Assembly.GetName().Name ?? string.Empty);

            if (assembly != null)
                yield return assembly;

        }

        private static Assembly? tryLoadAssembly(string assemblyName)
        {
            Assembly? loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.Ordinal));

            if (loadedAssembly != null)
                return loadedAssembly;

            try
            {
                return Assembly.Load(assemblyName);
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<Type> getLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.OfType<Type>();
            }
        }
    }
}
