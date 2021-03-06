﻿using System;
using System.Collections.Generic;
using UnityEngine;
using KSP;

public class ModuleRCSFX : ModuleRCS
{
    [KSPField]
    public bool fullThrust = false; // always use full thrust

    [KSPField()]
    string runningEffectName = "";
    [KSPField()]
    string engageEffectName = "";
    [KSPField()]
    string flameoutEffectName = "";

    public bool rcs_active;

    [KSPField()]
    public bool useZaxis = false;
    [KSPField]
    public bool enableYaw = true;
    [KSPField]
    public bool enablePitch = true;
    [KSPField]
    public bool enableRoll = true;

    [KSPField]
    public bool enableX = true;
    [KSPField]
    public bool enableY = true;
    [KSPField]
    public bool enableZ = true;

    [KSPField]
    public bool useThrottle = false;

    [KSPField]
    public bool correctThrust = true;

    public float maxIsp;


    public float mixtureFactor;

    public override void OnLoad(ConfigNode node)
    {
        if (!node.HasNode("PROPELLANT") && node.HasValue("resourceName") && (propellants == null || propellants.Count == 0))
        {
            ConfigNode c = new ConfigNode("PROPELLANT");
            c.SetValue("name", node.GetValue("resourceName"));
            c.SetValue("ratio", "1.0");
            if (node.HasValue("resourceFlowMode"))
                c.SetValue("resourceFlowMode", node.GetValue("resourceFlowMode"));
            node.AddNode(c);
        }
        base.OnLoad(node);
        G = 9.80665f;
        maxIsp = atmosphereCurve.Evaluate(0f);
    }

    public override string GetInfo()
    {
        string text = base.GetInfo();
        return text;
    }

    public override void OnStart(StartState state)
    {
        base.OnStart(state);
        maxIsp = atmosphereCurve.Evaluate(0f);
    }

    Vector3 inputLinear;
    Vector3 inputAngular;
    bool precision;

    new public void Update()
    {
        if (this.part.vessel == null)
            return;

        inputLinear = vessel.ReferenceTransform.rotation * new Vector3(enableX ? vessel.ctrlState.X : 0f, enableZ ? vessel.ctrlState.Z : 0f, enableY ? vessel.ctrlState.Y : 0f);
        inputAngular = vessel.ReferenceTransform.rotation * new Vector3(enablePitch ? vessel.ctrlState.pitch : 0f, enableRoll ? vessel.ctrlState.roll : 0f, enableYaw ? vessel.ctrlState.yaw : 0);
        if (useThrottle)
        {
            inputLinear.y -= vessel.ctrlState.mainThrottle;
            inputLinear.y = Mathf.Clamp(inputLinear.y, - 1f, 1f);
        }
        precision = FlightInputHandler.fetch.precisionMode;
    }

    new public void FixedUpdate()
    {
        if (HighLogic.LoadedSceneIsEditor)
            return;

        if (TimeWarp.CurrentRate > 1.0f && TimeWarp.WarpMode == TimeWarp.Modes.HIGH)
        {
            foreach (FXGroup fx in thrusterFX)
            {
                fx.setActive(false);
                fx.Power = 0f;
            }
            return;
        }

        bool success = false;
        realISP = atmosphereCurve.Evaluate((float)vessel.staticPressure);
        thrustForces.Clear();
        if (isEnabled && part.isControllable)
        {
            if (vessel.ActionGroups[KSPActionGroup.RCS] != rcs_active)
            {
                rcs_active = vessel.ActionGroups[KSPActionGroup.RCS];
                if(!(engageEffectName.Equals("")))
                    part.Effect(engageEffectName, 1.0f);
                if(!(runningEffectName.Equals("")))
                    part.Effect(runningEffectName, 0f);
                foreach (FXGroup fx in thrusterFX)
                {
                    fx.setActive(false);
                    fx.Power = 0f;
                }
            }
            if (vessel.ActionGroups[KSPActionGroup.RCS])
            {
                Vector3 CoM = vessel.CoM + vessel.rb_velocity * Time.deltaTime;

                float effectPower = 0f;
                for (int i = 0; i < thrusterTransforms.Count; i++)
                {
                    if (thrusterTransforms[i].position != Vector3.zero)
                    {
                        Vector3 position = thrusterTransforms[i].transform.position;
                        Vector3 torque = Vector3.Cross(inputAngular, (position - CoM).normalized);
                        Vector3 thruster;
                        if (useZaxis)
                            thruster = thrusterTransforms[i].forward;
                        else
                            thruster = thrusterTransforms[i].up;
                        float thrust = Mathf.Max(Vector3.Dot(thruster, torque), 0f);
                        thrust += Mathf.Max(Vector3.Dot(thruster, inputLinear), 0f);
                        if (thrust > 0 && fullThrust)
                            thrust = thrusterPower * ( precision ? 0.1f : 1f);

                        if (correctThrust)
                            thrust *= realISP / maxIsp;
                        if (thrust > 0.0001f)
                        {
                            if (precision && !fullThrust)
                            {
                                float arm = GetLeverDistance(-thruster, CoM);
                                if (arm > 1.0f)
                                    thrust = thrust / arm;
                            }
                            thrust = Mathf.Clamp(thrust, 0f, thrusterPower);
                            UpdatePropellantStatus();
                            thrust = CalculateThrust(thrust, out success);
                            thrustForces.Add(thrust);
                            if (success)
                            {
                                if (!isJustForShow)
                                {
                                    Vector3 force = (-1 * thrust) * thruster;

                                    part.Rigidbody.AddForceAtPosition(force, position, ForceMode.Force);
                                }

                                thrusterFX[i].Power = Mathf.Clamp(thrust / thrusterPower, 0.1f, 1f);
                                if (effectPower < thrusterFX[i].Power)
                                    effectPower = thrusterFX[i].Power;
                                thrusterFX[i].setActive(thrust > 0f);
                            }
                            else if (! (flameoutEffectName.Equals("")))
                                part.Effect(flameoutEffectName, 1.0f);
                        }
                    }
                }
                if(!(runningEffectName.Equals("")))
                    part.Effect(runningEffectName, effectPower);
            }
        }
        if (!success)
        {
            foreach (FXGroup fx in thrusterFX)
            {
                fx.setActive(false);
                fx.Power = 0f;
            }
        }

    }

    private void UpdatePropellantStatus()
    {
        if ((object)propellants != null)
            foreach (Propellant p in propellants)
                p.UpdateConnectedResources(part);
    }

}
