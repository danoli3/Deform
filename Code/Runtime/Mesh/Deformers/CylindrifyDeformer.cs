﻿using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace Deform
{
	[Deformer (Name = "Cylindrify", Description = "Blends mesh into a cylinder", XRotation = -90f, Type = typeof (CylindrifyDeformer))]
	public class CylindrifyDeformer : Deformer, IFactor
	{
		public float Factor
		{
			get => factor;
			set => factor = Mathf.Clamp01 (value);
		}
		public float Radius
		{
			get => radius;
			set => radius = value;
		}
		public Transform Axis
		{
			get
			{
				if (axis == null)
					axis = transform;
				return axis;
			}
			set => axis = value;
		}

		[SerializeField, HideInInspector] private float factor = 0f;
		[SerializeField, HideInInspector] private float radius = 1f;
		[SerializeField, HideInInspector] private Transform axis;

		public override DataFlags DataFlags => DataFlags.Vertices;

		public override JobHandle Process (MeshData data, JobHandle dependency = default (JobHandle))
		{
			if (Factor == 0f)
				return dependency;

			var meshToAxis = DeformerUtils.GetMeshToAxisSpace (Axis, data.Target.GetTransform ());

			return new CylindrifyDeformJob
			{
				factor = Factor,
				radius = Radius,
				meshToAxis = meshToAxis,
				axisToMesh = meshToAxis.inverse,
				vertices = data.DynamicNative.VertexBuffer
			}.Schedule (data.length, BatchCount, dependency);
		}

		[BurstCompile (CompileSynchronously = COMPILE_SYNCHRONOUSLY)]
		private struct CylindrifyDeformJob : IJobParallelFor
		{
			public float factor;
			public float radius;
			public float4x4 meshToAxis;
			public float4x4 axisToMesh;
			public NativeArray<float3> vertices;

			public void Execute (int index)
			{
				var point = mul (meshToAxis, float4 (vertices[index], 1f));

				var goalRadius = normalize (point.xy) * radius;

				point.xy = lerp (point.xy, goalRadius, factor);

				vertices[index] = mul (axisToMesh, point).xyz;
			}
		}
	}
}