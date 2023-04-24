using System.Diagnostics;
using TreesearchLib;

namespace HEAL.EquationSearch {

  /// <summary>
  /// A custom search control that prevents duplicate states.
  /// Used instead of the default search control from Treesearchlib.
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
      visitedNodesSet = new NonBlocking.ConcurrentDictionary<ulong, byte>();
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


    // The caches in GraphSearchControl and Evaluator have different purposes.
    // The cache in GraphSearchControl prevents visiting duplicate states in the state graph.
    // The cache in Evaluator prevents duplicate evaluations.
    // Currently, they are both necessary because GraphSearchControl calculates
    // semantic hashes for expressions with nonterminal symbols, while the cache in 
    // Evaluator only sees expressions where nonterminal symbols have been replaced by terminal symbols.
    private NonBlocking.ConcurrentDictionary<ulong,byte> visitedNodesSet; // a set would be sufficient. We need a concurrent set shared over all threads.

    private long visitedNodes = 0;
    public long VisitedNodes => visitedNodes; // this is the number of discarded + evaluated nodes

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
      visitedNodes++;
      var hash = state.GetHashValue();
      if (!visitedNodesSet.TryAdd(hash, 0)) {
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
      visitedNodes += other.VisitedNodes;
    }

    public static GraphSearchControl Start(State state) {
      return new GraphSearchControl(state);
    }

    public ISearchControl<State, MinimizeDouble> Fork(State state, bool withBestQuality, TimeSpan? maxTimeLimit = null) {
      var fork = new GraphSearchControl(state);
      fork.visitedNodesSet = visitedNodesSet; // we have to use the same (thread-safe!) set for de-duplication of states across threads
      fork.NodeLimit = NodeLimit - VisitedNodes;
      fork.Cancellation = Cancellation;
      fork.Runtime = Runtime - stopwatch.Elapsed;
      if (maxTimeLimit.HasValue && maxTimeLimit < fork.Runtime) {
        fork.Runtime = maxTimeLimit.Value;
      }
      if (withBestQuality) {
        fork.BestQuality = BestQuality;
        fork.BestQualityState = BestQualityState;
      }
      return fork;
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
