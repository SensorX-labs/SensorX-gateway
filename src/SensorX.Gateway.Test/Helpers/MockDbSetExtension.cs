using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;

namespace SensorX.Gateway.Test.Helpers;

/// <summary>
/// Extension methods for creating mock DbSet objects for testing
/// </summary>
public static class MockDbSetExtension
{
    /// <summary>
    /// Creates a mock DbSet from an IQueryable collection
    /// </summary>
    public static Mock<DbSet<T>> BuildMockDbSet<T>(this IQueryable<T> queryable) where T : class
    {
        var mockDbSet = new Mock<DbSet<T>>();

        mockDbSet.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new AsyncEnumerator<T>(queryable.GetEnumerator()));

        mockDbSet.As<IQueryable<T>>()
            .Setup(m => m.Provider)
            .Returns(new AsyncQueryProvider<T>(queryable.Provider));

        mockDbSet.As<IQueryable<T>>()
            .Setup(m => m.Expression)
            .Returns(queryable.Expression);

        mockDbSet.As<IQueryable<T>>()
            .Setup(m => m.ElementType)
            .Returns(queryable.ElementType);

        mockDbSet.As<IQueryable<T>>()
            .Setup(m => m.GetEnumerator())
            .Returns(queryable.GetEnumerator());

        mockDbSet.Setup(m => m.Add(It.IsAny<T>()))
            .Callback<T>(t => { })
            .Returns((T t) => null!);

        mockDbSet.Setup(m => m.AddAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .Returns((T t, CancellationToken ct) => new ValueTask<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T>>(new Mock<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T>>().Object));

        mockDbSet.Setup(m => m.Remove(It.IsAny<T>()))
            .Callback<T>(t => { });

        return mockDbSet;
    }
}

public class AsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _enumerator;

    public AsyncEnumerator(IEnumerator<T> enumerator)
    {
        _enumerator = enumerator;
    }

    public T Current => _enumerator.Current;

    public async ValueTask<bool> MoveNextAsync()
    {
        await Task.CompletedTask;
        return _enumerator.MoveNext();
    }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
        _enumerator.Dispose();
    }
}

public class AsyncQueryProvider<T> : IAsyncQueryProvider
{
    private readonly IQueryProvider _queryProvider;

    public AsyncQueryProvider(IQueryProvider queryProvider)
    {
        _queryProvider = queryProvider;
    }

    public IQueryable<TElement> CreateQuery<TElement>(System.Linq.Expressions.Expression expression)
    {
        return new AsyncQuery<TElement>(_queryProvider.CreateQuery<TElement>(expression));
    }

    public IQueryable CreateQuery(System.Linq.Expressions.Expression expression)
    {
        throw new NotImplementedException();
    }

    public TResult Execute<TResult>(System.Linq.Expressions.Expression expression)
    {
        return _queryProvider.Execute<TResult>(expression);
    }

    public object Execute(System.Linq.Expressions.Expression expression)
    {
        return _queryProvider.Execute(expression)!;
    }

    public TResult ExecuteAsync<TResult>(System.Linq.Expressions.Expression expression, CancellationToken cancellationToken = default)
    {
        var result = Execute<TResult>(expression);
        return result;
    }
}

public class AsyncQuery<T> : IAsyncEnumerable<T>, IQueryable<T>
{
    private readonly IQueryable<T> _queryable;

    public AsyncQuery(IQueryable<T> queryable)
    {
        _queryable = queryable;
    }

    public Type ElementType => _queryable.ElementType;

    public System.Linq.Expressions.Expression Expression => _queryable.Expression;

    public IQueryProvider Provider => _queryable.Provider;

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new AsyncEnumerator<T>(_queryable.GetEnumerator());
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _queryable.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return _queryable.GetEnumerator();
    }
}
