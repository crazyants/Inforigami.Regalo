using System;
using System.Collections.Generic;
using System.Linq;
using Inforigami.Regalo.Interfaces;
using NUnit.Framework;
using Inforigami.Regalo.Core.EventSourcing;
using Inforigami.Regalo.Core.Tests.DomainModel.Users;

namespace Inforigami.Regalo.Core.Tests.Unit
{
    [TestFixture]
    public class SimpleConcurrencyTests
    {
        [Test]
        public void GivenNoUnseenEventsAndNoUncommittedEvents_WhenTestingForConflicts_ThenFindNoConflicts()
        {
            // Arrange
            IConcurrencyMonitor monitor = new StrictConcurrencyMonitor();
            IEnumerable<IEvent> unseenEvents = Enumerable.Empty<IEvent>();
            IEnumerable<IEvent> uncommittedEvents = Enumerable.Empty<IEvent>();

            // Act
            IEnumerable<ConcurrencyConflict> conflicts = monitor.CheckForConflicts(unseenEvents, uncommittedEvents);

            // Assert
            CollectionAssert.IsEmpty(conflicts, "Conflicts have been returned where there should be none.");
        }
        
        [Test]
        public void GivenNoUnseenEvents_WhenTestingForConflicts_ThenFindNoConflicts()
        {
            // Arrange
            IConcurrencyMonitor monitor = new StrictConcurrencyMonitor();
            IEnumerable<IEvent> unseenEvents = Enumerable.Empty<IEvent>();
            IEnumerable<IEvent> uncommittedEvents = new[]
                                                        {
                                                            new UserRegistered(Guid.NewGuid())
                                                        };

            // Act
            IEnumerable<ConcurrencyConflict> conflicts = monitor.CheckForConflicts(unseenEvents, uncommittedEvents);

            // Assert
            CollectionAssert.IsEmpty(conflicts, "Conflicts have been returned where there should be none.");
        }

        [Test]
        public void GivenUnseenEventsAndNoUncommittedEvents_WhenTestingForConflicts_ThenFindNoConflicts()
        {
            // Arrange
            IConcurrencyMonitor monitor = new StrictConcurrencyMonitor();
            IEnumerable<IEvent> unseenEvents = new[] { new UserRegistered(Guid.NewGuid()) };
            IEnumerable<IEvent> uncommittedEvents = Enumerable.Empty<IEvent>();

            // Act
            IEnumerable<ConcurrencyConflict> conflicts = monitor.CheckForConflicts(unseenEvents, uncommittedEvents);

            // Assert
            CollectionAssert.IsEmpty(conflicts, "Conflicts have been returned where there should be none.");
        }

        [Test]
        public void GivenUnseenEvents_WhenTestingForConflicts_ThenFindConflicts()
        {
            // Arrange
            var userId = Guid.NewGuid();
            IConcurrencyMonitor monitor = new StrictConcurrencyMonitor();
            IEnumerable<IEvent> unseenEvents = new[] { new UserChangedPassword("newpassword") };
            IEnumerable<IEvent> uncommittedEvents = new[] { new UserChangedPassword("differentnewpassword") };

            // Act
            IList<ConcurrencyConflict> conflicts = monitor.CheckForConflicts(unseenEvents, uncommittedEvents).ToList();

            // Assert
            Assert.AreEqual(1, conflicts.Count, "Expected one and only one conflict.");
            var conflict = conflicts.Single();
            CollectionAssert.AreEqual(unseenEvents, conflict.UnseenEvents);
            CollectionAssert.AreEqual(uncommittedEvents, conflict.UncommittedEvents);
            Assert.AreEqual("Changes conflict with one or more committed events.", conflict.Message);
        }
    }
}
