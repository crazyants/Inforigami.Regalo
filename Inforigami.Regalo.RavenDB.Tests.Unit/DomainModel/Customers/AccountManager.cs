using System;
using Inforigami.Regalo.Core;

namespace Inforigami.Regalo.RavenDB.Tests.Unit.DomainModel.Customers
{
    public class AccountManager : AggregateRoot
    {
        private DateTime _startDate;

         public void Employ(DateTime startDate)
         {
             // Check the start date against known rules

             Record(new Employed(Guid.NewGuid(), startDate));
         }

        private void Apply(Employed evt)
        {
            Id = evt.EmployeeId;
            _startDate = evt.StartDate;
        }
    }
}
