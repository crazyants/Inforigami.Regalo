using Inforigami.Regalo.Core;

namespace Inforigami.Regalo.Testing
{
    public class Scenario
    {
        public static IHandlerSetter<TEntity> For<TEntity>(TestingMessageHandlerContext<TEntity> context)
            where TEntity : AggregateRoot, new()
        {
            return new ScenarioHandlerSetter<TEntity>(context);
        }
    }
}
