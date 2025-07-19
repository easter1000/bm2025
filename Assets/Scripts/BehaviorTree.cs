using System.Collections.Generic;

public enum NodeState
{
    SUCCESS,
    FAILURE,
    RUNNING
}

public abstract class Node
{
    protected List<Node> children = new List<Node>();
    public abstract NodeState Evaluate(GameSimulator simulator, GamePlayer player);
}

public class Selector : Node
{
    public Selector(List<Node> nodes) { children = nodes; }
    public override NodeState Evaluate(GameSimulator simulator, GamePlayer player)
    {
        foreach (var node in children)
        {
            switch (node.Evaluate(simulator, player))
            {
                case NodeState.SUCCESS: return NodeState.SUCCESS;
                case NodeState.FAILURE: continue;
                case NodeState.RUNNING: return NodeState.RUNNING;
            }
        }
        return NodeState.FAILURE;
    }
}

public class Sequence : Node
{
    public Sequence(List<Node> nodes) { children = nodes; }
    public override NodeState Evaluate(GameSimulator simulator, GamePlayer player)
    {
        foreach (var node in children)
        {
            switch (node.Evaluate(simulator, player))
            {
                case NodeState.SUCCESS: continue;
                case NodeState.FAILURE: return NodeState.FAILURE;
                case NodeState.RUNNING: return NodeState.RUNNING;
            }
        }
        return NodeState.SUCCESS;
    }
}

// 실제 행동을 정의하는 Leaf Node (추상 클래스)
public abstract class ActionNode : Node { }

// 조건을 검사하는 Leaf Node (추상 클래스)
public abstract class ConditionNode : Node { }