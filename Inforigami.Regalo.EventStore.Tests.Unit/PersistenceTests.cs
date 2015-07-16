﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using EventStore.ClientAPI;
using Inforigami.Regalo.Core;
using Inforigami.Regalo.Core.EventSourcing;
using Inforigami.Regalo.EventStore.Tests.Unit.DomainModel.Customers;
using Inforigami.Regalo.Interfaces;
using Inforigami.Regalo.Testing;
using NUnit.Framework;
using ILogger = Inforigami.Regalo.Core.ILogger;

namespace Inforigami.Regalo.EventStore.Tests.Unit
{
    [TestFixture]
    public class PersistenceTests
    {
        private IEventStoreConnection _eventStoreConnection;

        [SetUp]
        public void SetUp()
        {
            _eventStoreConnection = EventStoreConnection.Create(new IPEndPoint(IPAddress.Loopback, 1113));
            _eventStoreConnection.ConnectAsync().Wait();

            Resolver.Configure(type =>
            {
                if (type == typeof(ILogger)) return new NullLogger();
                throw new InvalidOperationException(string.Format("No type of {0} registered.", type));
            },
            type => null,
            o => { });
        }

        [TearDown]
        public void TearDown()
        {
            Conventions.SetFindAggregateTypeForEventType(null);

            Resolver.Reset();

            _eventStoreConnection.Close();
            _eventStoreConnection = null;
        }

        [Test]
        public void Loading_GivenEmptyStore_ShouldReturnNull()
        {
            // Arrange
            IEventStore store = new EventStoreEventStore(_eventStoreConnection);

            // Act
            EventStream<Customer> events = store.Load<Customer>(Guid.NewGuid().ToString());

            // Assert
            CollectionAssert.IsEmpty(events.Events);
        }

        [Test]
        public void Saving_GivenSingleEvent_ShouldAllowReloading()
        {
            // Arrange
            IEventStore store = new EventStoreEventStore(_eventStoreConnection);

            // Act
            var id = Guid.NewGuid();
            var evt = new CustomerSignedUp(id);
            store.Save<Customer>(id.ToString(), 0,  new[] { evt });
            var stream = store.Load<Customer>(id.ToString());

            // Assert
            Assert.NotNull(stream);
            CollectionAssert.AreEqual(
                new object[] { evt },
                stream.Events,
                "Events reloaded from store do not match those generated by aggregate.");
        }

        [Test]
        public void Saving_GivenEventWithGuidProperty_ShouldAllowReloadingToGuidType()
        {
            // Arrange
            IEventStore store = new EventStoreEventStore(_eventStoreConnection);

            var customer = new Customer();
            customer.Signup();

            var accountManager = new AccountManager();
            var startDate = new DateTime(2012, 4, 28);
            accountManager.Employ(startDate);

            customer.AssignAccountManager(accountManager.Id, startDate);

            store.Save<Customer>(customer.Id.ToString(), 0, customer.GetUncommittedEvents());

            // Act
            var acctMgrAssignedEvent = (AccountManagerAssigned)store.Load<Customer>(customer.Id.ToString())
                                                                    .Events
                                                                    .LastOrDefault();

            // Assert
            Assert.NotNull(acctMgrAssignedEvent);
            Assert.AreEqual(accountManager.Id, acctMgrAssignedEvent.AccountManagerId);
        }

        [Test]
        public void Saving_GivenEvents_ShouldAllowReloading()
        {
            // Arrange
            IEventStore store = new EventStoreEventStore(_eventStoreConnection);

            // Act
            var customer = new Customer();
            customer.Signup();
            store.Save<Customer>(customer.Id.ToString(), 0, customer.GetUncommittedEvents());
            var stream = store.Load<Customer>(customer.Id.ToString());

            // Assert
            Assert.NotNull(stream);
            CollectionAssert.AreEqual(customer.GetUncommittedEvents(), stream.Events, "Events reloaded from store do not match those generated by aggregate.");
        }


        [Test]
        public void Saving_GivenNoEvents_ShouldDoNothing()
        {
            // Arrange
            IEventStore store = new EventStoreEventStore(_eventStoreConnection);

            // Act
            var id = Guid.NewGuid();
            store.Save<Customer>(id.ToString(), 0, Enumerable.Empty<IEvent>());
            var stream = store.Load<Customer>(id.ToString());

            // Assert
            CollectionAssert.IsEmpty(stream.Events);
        }

        [Test]
        public void GivenAggregateWithMultipleEvents_WhenLoadingSpecificVersion_ThenShouldOnlyReturnRequestedEvents()
        {
            // Arrange
            IEventStore store = new EventStoreEventStore(_eventStoreConnection);
            var customerId = Guid.NewGuid();
            var storedEvents = new EventChain().Add(new CustomerSignedUp(customerId))
                                               .Add(new SubscribedToNewsletter("latest"))
                                               .Add(new SubscribedToNewsletter("top"));
            store.Save<Customer>(customerId.ToString(), 0, storedEvents);

            // Act
            var stream = store.Load<Customer>(customerId.ToString(), storedEvents[1].Headers.Version);

            // Assert
            CollectionAssert.AreEqual(storedEvents.Take(2), stream.Events, "Events loaded from store do not match version requested.");
        }

        [Test]
        public void GivenAggregateWithMultipleEvents_WhenLoadingSpecificVersionThatNoEventHas_ThenShouldFail()
        {
            // Arrange
            IEventStore store = new EventStoreEventStore(_eventStoreConnection);
            var customerId = Guid.NewGuid();
            var storedEvents = new IEvent[]
                              {
                                  new CustomerSignedUp(customerId), 
                                  new SubscribedToNewsletter("latest"), 
                                  new SubscribedToNewsletter("top")
                              };
            store.Save<Customer>(customerId.ToString(), 0, storedEvents);

            // Act / Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => store.Load<Customer>(customerId.ToString(), 4));
        }
    }
}