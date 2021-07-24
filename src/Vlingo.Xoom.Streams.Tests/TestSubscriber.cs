using System;
using System.Collections.Concurrent;
using Reactive.Streams;
using Vlingo.Xoom.Actors.TestKit;
using Vlingo.Xoom.Common;

namespace Vlingo.Xoom.Streams.Tests
{
  public class TestSubscriber<T> : ISubscriber<T>
  {
    private AccessSafely _access;

    private readonly AtomicInteger _onSubscribeCount = new (0);
    private readonly AtomicInteger _onNextCount = new (0);
    private readonly AtomicInteger _onErrorCount = new (0);
    private readonly AtomicInteger _onCompleteCount = new (0);

    private readonly BlockingCollection<T> _values = new();

    private readonly Sink<T> _sink;

    private bool _cancelled;
    private readonly int _cancelAfterElements;
    private readonly int _total;
    private ISubscription _subscription;

    public TestSubscriber(int total) : this(total, total * total)
    {
    }

    public TestSubscriber(int total, int cancelAfterElements) : this(null, total, cancelAfterElements)
    {
    }

    public TestSubscriber(Sink<T> sink, int total, int cancelAfterElements)
    {
      _sink = sink;
      _total = total;
      _cancelAfterElements = cancelAfterElements;

      _access = AfterCompleting(0);
    }

    public void OnSubscribe(ISubscription subscription)
    {
      if (_subscription != null)
      {
        subscription.Cancel();
        return;
      }

      _subscription = subscription;
      _access.WriteUsing("onSubscribe", 1);
      subscription.Request(_total);

      _sink?.Ready();
    }

    public void OnNext(T value)
    {
      _access.WriteUsing("onNext", 1);
      _access.WriteUsing("values", value);

      if (_onNextCount.Get() >= _cancelAfterElements && !_cancelled)
      {
        _subscription?.Cancel();
        _sink?.Terminate();
        _cancelled = true;
      }

      _sink?.WhenValue(value);
    }

    public void OnError(Exception cause)
    {
      _access.WriteUsing("onError", 1);
      _sink?.Terminate();
    }

    public void OnComplete()
    {
      _access.WriteUsing("onComplete", 1);
      _sink?.Terminate();
    }

    public AccessSafely AfterCompleting(int times)
    {
      _access = AccessSafely.AfterCompleting(times);

      _access.WritingWith("onSubscribe", (int value) => _onSubscribeCount.AddAndGet(value));
      _access.WritingWith("onNext", (int value) => _onNextCount.AddAndGet(value));
      _access.WritingWith("onError", (int value) => _onErrorCount.AddAndGet(value));
      _access.WritingWith("onComplete", (int value) => _onCompleteCount.AddAndGet(value));

      _access.WritingWith("values", (T values) => _values.Add(values));

      _access.ReadingWith("onSubscribe", () => _onSubscribeCount.Get());
      _access.ReadingWith("onNext", () => _onNextCount.Get());
      _access.ReadingWith("onError", () => _onErrorCount.Get());
      _access.ReadingWith("onComplete", () => _onCompleteCount.Get());

      _access.ReadingWith("values", () => _values);

      return _access;
    }
  }
}