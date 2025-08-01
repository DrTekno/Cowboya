using UnityEngine;
using System.Collections;

[RequireComponent(typeof(WorkerStateMachine), typeof(RobotMemory))]
public class EnemyWorkerController : AnimatorBaseAgentController
{
    [SerializeField] public WorkerStateMachine stateMachine;
    [SerializeField] private RobotMemory memoryComponent;

    [SerializeField] private FacingController facing;
    [SerializeField] private LegJointLimiter legJointLimiter;
    [SerializeField] private BodyJointLimiter bodyJointLimiter;

    private bool flipped = false;

    private IWorkerStateMachine stateMachineInterface;
    public IRobotMemory memory { get; private set; }
    private WaypointPathFollower pathFollower;
    private IWaypointQueries waypointQueries;
    public IWaypointService waypointService;

    [SerializeField] private RobotStateController robotBehaviour;
    [SerializeField] private float arrivalThresholdX = 2f;
    [SerializeField] private float arrivalThresholdY = 2f;
    [SerializeField] private float deadZoneX = 5f;
    [SerializeField] private float deadZoneY = 5f;

    public WorkerStatus workerState { get; set; } = WorkerStatus.Idle;

    public bool IsWorkerSpawner { get; private set; }

    [SerializeField] private UpdateLoop updateLoop = UpdateLoop.Update;

    private void Awake()
    {
        if (stateMachine == null)
            stateMachine = GetComponent<WorkerStateMachine>();
        stateMachineInterface = stateMachine;

        if (memoryComponent == null)
            memoryComponent = GetComponent<RobotMemory>();
        memory = memoryComponent;

        animator = GetComponentInChildren<Animator>();

        if (facing == null)
            facing = GetComponent<FacingController>();
        if (legJointLimiter == null)
            legJointLimiter = GetComponent<LegJointLimiter>();
        // if (bodyJointLimiter == null)
        //     bodyJointLimiter = GetComponent<BodyJointLimiter>();

        robotBehaviour.OnStateChanged += HandleStateChange;
    }

    public void Initialize(IWaypointQueries waypointQueries, IWaypointService waypointService, IRobotRespawnService respawnService)
    {
        this.waypointQueries = waypointQueries;
        this.waypointService = waypointService;
        pathFollower = new WaypointPathFollower(bodyReference, this, waypointQueries,
            arrivalThresholdX, arrivalThresholdY, deadZoneX, deadZoneY);
        pathFollower.OnStuck += () => memory.OnStuck(this);
        waypointService.Subscribe(pathFollower);
        memory.SetRespawnService(respawnService);
        stateMachine.ChangeState(new Worker_Idle(this, stateMachine, (IWaypointService)waypointQueries));
    }

    public void SetWorkerSpawnerState()
    {
        IsWorkerSpawner = true;
    }

    protected override void Update()
    {
        base.Update();
        TryFlip(direction);
        if (updateLoop == UpdateLoop.Update)
            pathFollower?.Update(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (updateLoop == UpdateLoop.FixedUpdate)
            pathFollower?.Update(Time.fixedDeltaTime);
    }


    public void SetDestination(RoomWaypoint target, bool includeUnavailable = false) =>
        pathFollower.SetDestination(target, includeUnavailable);

    public bool HasArrivedAtDestination() => pathFollower.HasArrived;

    public void OnPathObsoleted(RoomWaypoint blockedWaypoint) =>
        pathFollower.OnPathObsoleted(blockedWaypoint);

    public RoomWaypoint GetClosestWaypoint(RoomWaypoint exclude = null) =>
        pathFollower.GetClosestWaypoint(exclude);

    private void TryFlip(float input)
    {
        if (Mathf.Abs(input) > 0.1f)
        {
            bool movingLeft = input < 0f;
            if (movingLeft != flipped)
            {
                flipped = movingLeft;
                ApplyFacingDirection();
            }
        }
    }

    private void ApplyFacingDirection()
    {
        if (facing != null)
            facing.SetLegFacing(!flipped);
        if (legJointLimiter != null)
            legJointLimiter.SetLegRotationLimits(flipped);
        if (bodyJointLimiter != null)
            bodyJointLimiter.SetBodyRotationLimits(flipped);
    }

    private void HandleStateChange(RobotState newState)
    {
        switch (newState)
        {
            case RobotState.Faint:
                Faint();
                break;
            case RobotState.Dead:
                Die();
                break;
            case RobotState.Alive:
                UpdateBalance(true);
                break;
        }
    }

    public void Faint()
    {
        UpdateBalance(false);
    }

    public void Die()
    {
        var jointBreaker = GetComponent<JointBreaker>();
        jointBreaker?.BreakAll();
        SceneController.instance.RobotKilled();

        StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        yield return new WaitForSeconds(5f);
        ObjectPool.Instance.Release(gameObject);
    }

    private void DisableAnimator()
    {
        if (animator != null)
        {
            animator.enabled = false;
        }
    }

    private void UpdateBalance(bool enabledBalance)
    {
        var bodyBalance = GetComponent<BodyBalance>();
        if (bodyBalance != null)
        {
            bodyBalance.UpdateBalance(enabledBalance);
        }
    }

    private void OnDrawGizmos()
    {
        pathFollower?.DrawGizmos();
    }
}
