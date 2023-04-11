using System.Collections.Concurrent;
using System.Diagnostics;
using TreesearchLib;

namespace HEAL.EquationSearch {

  /// <summary>
  /// A custom search control that prevents duplicate states.
  /// </summary>
  public class GraphSearchControl : ISearchControl<State, MinimizeDouble> {
    private GraphSearchControl(State state) {
      stopwatch = Stopwatch.StartNew();
      InitialState = state;
      BestQuality = null;
      BestQualityState = state;
      Cancellation = CancellationToken.None;
      Runtime = TimeSpan.MaxValue;
      NodeLimit = long.MaxValue;
      visitedNodesSet = new ConcurrentDictionary<ulong, ulong>();
    }

    private readonly Stopwatch stopwatch;

    public QualityCallback<State, MinimizeDouble> ImprovementCallback { get; set; }

    public State InitialState { get; set; }
    public MinimizeDouble? BestQuality { get; set; }
    public State BestQualityState { get; set; }

    public TimeSpan Elapsed => stopwatch.Elapsed;
    public TimeSpan Runtime { get; set; }
    public CancellationToken Cancellation { get; set; }
    public long NodeLimit { get; set; }

    private readonly ConcurrentDictionary<ulong, ulong> visitedNodesSet; // a set would be sufficient, TODO: do we need concurrency here?

    public long VisitedNodes => visitedNodesSet.Count;

    public bool IsFinished => !stopwatch.IsRunning;

    public GraphSearchControl Finish() {
      stopwatch.Stop();
      return this;
    }

    public bool ShouldStop() {
      if (IsFinished || Cancellation.IsCancellationRequested || stopwatch.Elapsed > Runtime
          || VisitedNodes >= NodeLimit) {
        return true;
      }

      return false;
    }

    public VisitResult VisitNode(State state) {
      var hash = state.GetHashValue();
      if (!visitedNodesSet.TryAdd(hash, hash)) {
        return VisitResult.Discard;
      } else {

        var result = BestQuality.HasValue && !state.Bound.IsBetter(BestQuality.Value) ? VisitResult.Discard : VisitResult.Ok;

        var quality = state.Quality;
        if (quality.HasValue) {
          if (!BestQuality.HasValue || quality.Value.IsBetter(BestQuality.Value)) {
            BestQuality = quality;
            BestQualityState = (State)state.Clone();
            ImprovementCallback?.Invoke(this, state, quality.Value);
          }
        }
        return result;

      }
    }

    public void Merge(ISearchControl<State, MinimizeDouble> other) {
      if (other.BestQuality.HasValue) {
        if (!BestQuality.HasValue || other.BestQuality.Value.IsBetter(BestQuality.Value)) {
          BestQuality = other.BestQuality;
          BestQualityState = other.BestQualityState; // is already a clone
          ImprovementCallback?.Invoke(this, other.BestQualityState, other.BestQuality.Value);
        }
      }
      var otherGraphSearchControl = other as GraphSearchControl;
      foreach (var tup in otherGraphSearchControl.visitedNodesSet) {
        visitedNodesSet.TryAdd(tup.Key, tup.Value);
      }
    }

    public static GraphSearchControl Start(State state) {
      return new GraphSearchControl(state);
    }
  }

  public static class SearchControlExtensions {
    public static GraphSearchControl WithImprovementCallback(this GraphSearchControl control, QualityCallback<State, MinimizeDouble> callback) {
      control.ImprovementCallback = callback;
      return control;
    }

    public static GraphSearchControl WithCancellationToken(this GraphSearchControl control, CancellationToken token) {
      control.Cancellation = token;
      return control;
    }

    public static GraphSearchControl WithBound(this GraphSearchControl control, MinimizeDouble bound) {
      control.BestQuality = bound;
      return control;
    }

    public static GraphSearchControl WithRuntimeLimit(this GraphSearchControl control, TimeSpan runtime) {
      control.Runtime = runtime;
      return control;
    }

    public static GraphSearchControl WithNodeLimit(this GraphSearchControl control, long nodelimit) {
      control.NodeLimit = nodelimit;
      return control;
    }
  }
}
