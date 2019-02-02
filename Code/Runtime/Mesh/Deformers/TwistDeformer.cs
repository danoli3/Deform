﻿using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace Deform
{
	[Deformer (Name = "Twist", Description = "Rotates vertices around an axis based on distance along that axis", XRotation = -90f, Type = typeof (TwistDeformer))]
	public class TwistDeformer : Deformer, IFactor
	{
		private const float MIN_RANGE = 0.001f;

		public float StartAngle
		{
			get => startAngle;
			set => startAngle = value;
		}
		public float EndAngle
		{
			get => endAngle;
			set => endAngle = value;
		}
		public float Offset
		{
			get => offset;
			set => offset = value;
		}
		public float Factor
		{
			get => factor;
			set => factor = value;
		}
		public BoundsMode Mode
		{
			get => mode;
			set => mode = value;
		}
		public bool Smooth
		{
			get => smooth;
			set => smooth = value;
		}
		public float Top
		{
			get => top;
			set => top = Mathf.Max (value, Bottom);
		}
		public float Bottom
		{
			get => bottom;
			set => bottom = Mathf.Min (value, Top);
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

		[SerializeField, HideInInspector] private float startAngle = 0f;
		[SerializeField, HideInInspector] private float endAngle = 0f;
		[SerializeField, HideInInspector] private float offset;
		[SerializeField, HideInInspector] private float factor = 1f;
		[SerializeField, HideInInspector] private BoundsMode mode = BoundsMode.Limited;
		[SerializeField, HideInInspector] private bool smooth = true;
		[SerializeField, HideInInspector] private float top = 0.5f;
		[SerializeField, HideInInspector] private float bottom = -0.5f;
		[SerializeField, HideInInspector] private Transform axis;

		public override DataFlags DataFlags => DataFlags.Vertices;

		public override JobHandle Process (MeshData data, JobHandle dependency = default (JobHandle))
		{
			if (Factor == 0f)
				return dependency;

			var meshToAxis = DeformerUtils.GetMeshToAxisSpace (Axis, data.Target.GetTransform ());

			switch (mode)
			{
				default:
					return new UnlimitedTwistDeformJob
					{
						startAngle = StartAngle * Factor + Offset,
						endAngle = EndAngle * Factor + Offset,
						top = Top,
						bottom = Bottom,
						meshToAxis = meshToAxis,
						axisToMesh = meshToAxis.inverse,
						vertices = data.DynamicNative.VertexBuffer
					}.Schedule (data.length, BatchCount, dependency);
				case BoundsMode.Limited:
					return new LimitedTwistDeformJob
					{
						startAngle = StartAngle * Factor + Offset,
						endAngle = EndAngle * Factor + Offset,
						top = Top,
						bottom = Bottom,
						smooth = Smooth,
						meshToAxis = meshToAxis,
						axisToMesh = meshToAxis.inverse,
						vertices = data.DynamicNative.VertexBuffer
					}.Schedule (data.length, BatchCount, dependency);
			}
		}

		[BurstCompile (CompileSynchronously = COMPILE_SYNCHRONOUSLY)]
		private struct UnlimitedTwistDeformJob : IJobParallelFor
		{
			public float startAngle;
			public float endAngle;
			public float top;
			public float bottom;
			public float4x4 meshToAxis;
			public float4x4 axisToMesh;
			public NativeArray<float3> vertices;

			public void Execute (int index)
			{
				var range = abs (top - bottom);
				if (range < MIN_RANGE)
					top += MIN_RANGE;

				var point = mul (meshToAxis, float4 (vertices[index], 1f));
				var degrees = (point.z / range) * (endAngle - startAngle);

				var rads = radians (startAngle + degrees) + (float)PI;
				point.xy = float2 
				(
					-point.x * cos (rads) - point.y * sin (rads), 
					point.x * sin (rads) - point.y * cos (rads)
				);

				vertices[index] = mul (axisToMesh, point).xyz;
			}
		}

		[BurstCompile (CompileSynchronously = COMPILE_SYNCHRONOUSLY)]
		private struct LimitedTwistDeformJob : IJobParallelFor
		{
			public float startAngle;
			public float endAngle;
			public float top;
			public float bottom;
			public bool smooth;
			public float4x4 meshToAxis;
			public float4x4 axisToMesh;
			public NativeArray<float3> vertices;

			public void Execute (int index)
			{
				var range = abs (top - bottom);
				if (range < MIN_RANGE)
					return;

				var angleDifference = endAngle - startAngle;

				var point = mul (meshToAxis, float4 (vertices[index], 1f));

				var degrees = 0f;
				if (smooth)
					degrees = startAngle + smoothstep (bottom, top, clamp (point.z, bottom, top)) * angleDifference;
				else
					degrees = lerp (startAngle, endAngle, (clamp (point.z, bottom, top) - bottom) / range);

				var rads = radians (degrees) + (float)PI;
				point.xy = float2
				(
					-point.x * cos (rads) - point.y * sin (rads),
					point.x * sin (rads) - point.y * cos (rads)
				);

				vertices[index] = mul (axisToMesh, point).xyz;
			}
		}
	}
}