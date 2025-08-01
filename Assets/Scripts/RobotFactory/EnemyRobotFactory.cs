using System;
using System.Collections.Generic;
using UnityEngine;

// Enemy Robot Factory
public class EnemyRobotFactory : RobotFactory
{
    public EnemyRobotFactory()
    {
        health = 20;
        energy = 30;
        morality = -10;
        energyAttackCost = 5;
    }

    public override RobotStats CreateRobot()
    {
        return new RobotStats(health, health, energy, energy, energyAttackCost,morality, new List<Module>(modules), new List<Attack>(attacks));
    }
}