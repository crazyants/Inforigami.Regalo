using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Inforigami.Regalo.Interfaces;

namespace Inforigami.Regalo.Core
{
    public abstract class AggregateRoot
    {
        private static readonly ILogger __logger = Resolver.Resolve<ILogger>();
        
        private readonly IDictionary<RuntimeTypeHandle, MethodInfo> _applyMethodCache = new Dictionary<RuntimeTypeHandle, MethodInfo>();
        private readonly IList<IEvent> _uncommittedEvents = new List<IEvent>();

        public Guid Id { get; protected set; }

        /// <summary>
        /// The version of the aggregate when loaded from the store.
        /// </summary>
        public int BaseVersion { get; private set; }

        /// <summary>
        /// The current version of the aggregate taking into account any uncommitted events it has generated.
        /// </summary>
        public int Version { get; private set; }

        protected AggregateRoot()
        {
            BaseVersion = -1;
            Version = -1;
        }

        public IEnumerable<IEvent> GetUncommittedEvents()
        {
            return _uncommittedEvents.ToList();
        }

        public void AcceptUncommittedEvents()
        {
            if (_uncommittedEvents.Any() == false)
            {
                Debug("No uncommitted events to accept");
                return;
            }

            __logger.Debug(this, "Accepting uncommitted events");

            BaseVersion = Version;
            int eventCount = _uncommittedEvents.Count;
            _uncommittedEvents.Clear();
            Debug("Accepted {0} uncommitted events. Now at base version {1}", eventCount, BaseVersion);
        }

        public void ApplyAll(IEnumerable<IEvent> events)
        {
            var eventList = events as IList<IEvent> ?? events.ToList();

            foreach (var evt in eventList)
            {
                ApplyEvent(evt);
            }

            BaseVersion = Version;

            Debug("Applied {0} events. Now at base version {1}", eventList.Count, BaseVersion);
        }

        protected void Record(IEvent evt)
        {
            Debug("Recording new event: {0}", evt);

            evt.Version = Version + 1;

            ApplyEvent(evt);

            ValidateHasId();

            _uncommittedEvents.Add(evt);
        }

        private void ApplyEvent(IEvent evt)
        {
            Version = evt.Version;

            var applyMethods = FindApplyMethods(evt);

            var applyMethodNames = string.Join(", ", applyMethods.Select(x => x.ToString()));
            Debug("Applying {0} via {1}...", evt.GetType(), applyMethodNames);

            foreach (var applyMethod in applyMethods)
            {
                applyMethod.Invoke(this, new[] { evt });
            }
        }

        private void ValidateHasId()
        {
            if (Id == default(Guid))
            {
                throw new IdNotSetException();
            }
        }

        /// <summary>
        /// Returns the Apply methods for each type in the event type's inheritance
        /// hierarchy, top-down, from interfaces before classes.
        /// </summary>
        private IEnumerable<MethodInfo> FindApplyMethods(object evt)
        {
            var typeInspector = new TypeInspector();

            var typeHierarchy = typeInspector.GetTypeHierarchy(evt.GetType());

            var applyMethods = typeHierarchy.Select(FindApplyMethod)
                                            .Where(x => x != null)
                                            .ToList();

            if (Conventions.AggregatesMustImplementApplyMethods && applyMethods.Count == 0)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "Class {0} does not implement an Apply() method for {1} or any of it's superclasses. Either implement the method or set Conventions.AggregatesMustImplementApplyMethods to false.",
                        GetType().Name,
                        evt.GetType()));
            }

            return applyMethods;
        }

        private MethodInfo FindApplyMethod(Type eventType)
        {
            var typeInspector = new TypeInspector();

            MethodInfo applyMethod;
            if (false == _applyMethodCache.TryGetValue(eventType.TypeHandle, out applyMethod))
            {
                applyMethod =
                    typeInspector.GetTypeHierarchy(GetType())
                                 .SelectMany(x => x.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
                                 .Where(m => m.Name == "Apply")
                                 .Where(
                                     m =>
                                     {
                                         var parameters = m.GetParameters();
                                         return parameters.Length == 1 && parameters[0].ParameterType == eventType;
                                     }).SingleOrDefault();

                _applyMethodCache.Add(eventType.TypeHandle, applyMethod);
            }

            return applyMethod;
        }

        private void Debug(string format, params object[] values)
        {
            var message = string.Format(format, values);
            __logger.Debug(this, "{0}@{1}<{2}: {3}", Id, BaseVersion, Version, message);
        }
    }
}
