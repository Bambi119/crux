using System;
using System.Collections.Generic;
using Crux.Data;
using UnityEngine;

namespace Crux.UI.Hangar
{
    // docs/10b §4.2 — 격납고 모듈 간 이벤트 버스.
    // 규칙: 동기 디스패치, 구독자 간 순서 보장 없음, 예외는 구독자별로 격리(로그 후 계속).
    // 재진입(이벤트 핸들러에서 같은 이벤트 Publish) 금지 — 감지 시 예외.

    public interface IHangarBus
    {
        void Publish<T>(in T evt) where T : struct;
        void Subscribe<T>(Action<T> handler) where T : struct;
        void Unsubscribe<T>(Action<T> handler) where T : struct;
    }

    public class HangarBus : IHangarBus
    {
        readonly Dictionary<Type, Delegate> handlers = new Dictionary<Type, Delegate>();
        readonly HashSet<Type> dispatching = new HashSet<Type>();

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            var key = typeof(T);
            if (handlers.TryGetValue(key, out var existing))
            {
                handlers[key] = Delegate.Combine(existing, handler);
            }
            else
            {
                handlers[key] = handler;
            }
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;
            var key = typeof(T);
            if (!handlers.TryGetValue(key, out var existing)) return;
            var remaining = Delegate.Remove(existing, handler);
            if (remaining == null) handlers.Remove(key);
            else handlers[key] = remaining;
        }

        public void Publish<T>(in T evt) where T : struct
        {
            var key = typeof(T);
            if (!dispatching.Add(key))
            {
                Debug.LogError($"[CRUX] [HANGAR] 재진입 감지: {key.Name} 핸들러 내에서 같은 이벤트 Publish 금지");
                return;
            }

            try
            {
                if (!handlers.TryGetValue(key, out var del)) return;
                var list = del.GetInvocationList();
                for (int i = 0; i < list.Length; i++)
                {
                    try
                    {
                        ((Action<T>)list[i]).Invoke(evt);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[CRUX] [HANGAR] 구독자 예외 {key.Name} → {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
            finally
            {
                dispatching.Remove(key);
            }
        }
    }

    // === 이벤트 타입 — docs/10b §4.2 9종 ===

    public readonly struct TankSelectedEvent
    {
        public readonly TankInstance Tank;
        public TankSelectedEvent(TankInstance tank) { Tank = tank; }
    }

    public readonly struct PartEquippedEvent
    {
        public readonly TankInstance Tank;
        public readonly PartInstance Part;
        public readonly PartCategory Category;
        public PartEquippedEvent(TankInstance tank, PartInstance part, PartCategory category)
        {
            Tank = tank; Part = part; Category = category;
        }
    }

    public readonly struct PartUnequippedEvent
    {
        public readonly TankInstance Tank;
        public readonly PartInstance Part;
        public readonly PartCategory Category;
        public PartUnequippedEvent(TankInstance tank, PartInstance part, PartCategory category)
        {
            Tank = tank; Part = part; Category = category;
        }
    }

    public readonly struct CrewAssignedEvent
    {
        public readonly TankInstance Tank;
        public readonly CrewMemberRuntime Crew;
        public readonly CrewClass Role;
        public CrewAssignedEvent(TankInstance tank, CrewMemberRuntime crew, CrewClass role)
        {
            Tank = tank; Crew = crew; Role = role;
        }
    }

    public readonly struct CrewUnassignedEvent
    {
        public readonly TankInstance Tank;
        public readonly CrewMemberRuntime Crew;
        public readonly CrewClass Role;
        public CrewUnassignedEvent(TankInstance tank, CrewMemberRuntime crew, CrewClass role)
        {
            Tank = tank; Crew = crew; Role = role;
        }
    }

    public readonly struct TraitsRecalculatedEvent
    {
        public readonly TankInstance Tank;
        public readonly IReadOnlyList<TraitModifier> Traits;
        public TraitsRecalculatedEvent(TankInstance tank, IReadOnlyList<TraitModifier> traits)
        {
            Tank = tank; Traits = traits;
        }
    }

    public readonly struct TabChangedEvent
    {
        public readonly HangarTab Previous;
        public readonly HangarTab Current;
        public TabChangedEvent(HangarTab previous, HangarTab current)
        {
            Previous = previous; Current = current;
        }
    }

    public readonly struct AwakeningQueueChangedEvent
    {
        public readonly int Count;
        public AwakeningQueueChangedEvent(int count) { Count = count; }
    }

    public readonly struct LaunchConfirmedEvent
    {
        public readonly IReadOnlyList<TankInstance> Loadout;
        public LaunchConfirmedEvent(IReadOnlyList<TankInstance> loadout) { Loadout = loadout; }
    }
}
