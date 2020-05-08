﻿// Crest Ocean System

// This file is subject to the MIT License as seen in the root of this folder structure (LICENSE)

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;
#endif

namespace Crest
{
    /// <summary>
    /// Handles effects that need to track the water surface. Feeds in wave data and disables rendering when
    /// not close to water.
    /// </summary>
    public partial class UnderwaterEffect : MonoBehaviour
    {
        [Header("Copy params from Ocean material")]

        [Tooltip("Copy ocean material settings on startup, to ensure consistent appearance between underwater effect and ocean surface."), SerializeField]
        bool _copyParamsOnStartup = true;
        [Tooltip("Copy ocean material settings on each frame, to ensure consistent appearance between underwater effect and ocean surface. This should be turned off if you are not changing the ocean material values every frame."), SerializeField]
        bool _copyParamsEachFrame = true;

        [Header("Advanced")]

        [Tooltip("This GameObject will be disabled when view height is more than this much above the water surface."), SerializeField]
        float _maxHeightAboveWater = 1.5f;
        [Tooltip("Override the default Unity draw order."), SerializeField]
        bool _overrideSortingOrder = false;
        [Tooltip("If the draw order override is enabled use this new order value."), SerializeField]
        int _overridenSortingOrder = 0;

        // how many vertical edges to add to curtain geometry
        const int GEOM_HORIZ_DIVISIONS = 64;

        PropertyWrapperMPB _mpb;
        Renderer _rend;

        readonly int sp_HeightOffset = Shader.PropertyToID("_HeightOffset");

        SampleHeightHelper _sampleWaterHeight = new SampleHeightHelper();

        private void Start()
        {
            _rend = GetComponent<Renderer>();

            // Render before the surface mesh
            _rend.sortingOrder = _overrideSortingOrder ? _overridenSortingOrder : -LodDataMgr.MAX_LOD_COUNT - 1;
            GetComponent<MeshFilter>().mesh = Mesh2DGrid(0, 2, -0.5f, -0.5f, 1f, 1f, GEOM_HORIZ_DIVISIONS, 1);

#if UNITY_EDITOR
            if (!Validate(OceanRenderer.Instance, ValidatedHelper.DebugLog))
            {
                enabled = false;
                return;
            }
#endif

            // hack - push forward so the geometry wont be frustum culled. there might be better ways to draw
            // this stuff.
            transform.localPosition = Vector3.forward;

            ConfigureMaterial();
        }

        void ConfigureMaterial()
        {
            if (OceanRenderer.Instance == null) return;

            if (_copyParamsOnStartup)
            {
                _rend.material.CopyPropertiesFromMaterial(OceanRenderer.Instance.OceanMaterial);
            }
        }

        private void LateUpdate()
        {
            if (OceanRenderer.Instance == null)
            {
                _rend.enabled = false;
                return;
            }

            float waterHeight = OceanRenderer.Instance.SeaLevel;
            _sampleWaterHeight.Init(transform.position, 0f);
            _sampleWaterHeight.Sample(ref waterHeight);

            float heightOffset = transform.position.y - waterHeight;

            // Disable skirt when camera not close to water. In the first few frames collision may not be avail, in that case no choice
            // but to assume enabled. In the future this could detect if camera is far enough under water, render a simple quad to avoid
            // finding the intersection line.
            _rend.enabled = heightOffset < _maxHeightAboveWater;

            if (_rend.enabled)
            {
                if (_copyParamsEachFrame)
                {
                    _rend.material.CopyPropertiesFromMaterial(OceanRenderer.Instance.OceanMaterial);
                }

                // Assign lod0 shape - trivial but bound every frame because lod transform comes from here
                if (_mpb == null)
                {
                    _mpb = new PropertyWrapperMPB();
                }
                _rend.GetPropertyBlock(_mpb.materialPropertyBlock);

                // Underwater rendering uses displacements for intersecting the waves with the near plane, and ocean depth/shadows for ScatterColour()
                _mpb.SetInt(LodDataMgr.sp_LD_SliceIndex, 0);
                OceanRenderer.Instance._lodDataAnimWaves.BindResultData(_mpb);

                if (OceanRenderer.Instance._lodDataSeaDepths)
                {
                    OceanRenderer.Instance._lodDataSeaDepths.BindResultData(_mpb);
                }
                else
                {
                    LodDataMgrSeaFloorDepth.BindNull(_mpb);
                }

                if (OceanRenderer.Instance._lodDataShadow)
                {
                    OceanRenderer.Instance._lodDataShadow.BindResultData(_mpb);
                }
                else
                {
                    LodDataMgrShadow.BindNull(_mpb);
                }

                _mpb.SetFloat(sp_HeightOffset, heightOffset);

                _mpb.SetVector(OceanChunkRenderer.sp_InstanceData, new Vector3(OceanRenderer.Instance.ViewerAltitudeLevelAlpha, 0f, 0f));

                _rend.SetPropertyBlock(_mpb.materialPropertyBlock);
            }
        }

        static Mesh Mesh2DGrid(int dim0, int dim1, float start0, float start1, float width0, float width1, int divs0, int divs1)
        {
            Vector3[] verts = new Vector3[(divs1 + 1) * (divs0 + 1)];
            Vector2[] uvs = new Vector2[(divs1 + 1) * (divs0 + 1)];
            float dx0 = width0 / divs0, dx1 = width1 / divs1;
            for (int i1 = 0; i1 < divs1 + 1; i1++)
            {
                float v = i1 / (float)divs1;

                for (int i0 = 0; i0 < divs0 + 1; i0++)
                {
                    int i = (divs0 + 1) * i1 + i0;
                    verts[i][dim0] = start0 + i0 * dx0;
                    verts[i][dim1] = start1 + i1 * dx1;

                    uvs[i][0] = i0 / (float)divs0;
                    uvs[i][1] = v;
                }
            }

            int[] indices = new int[divs0 * divs1 * 2 * 3];
            for (int i1 = 0; i1 < divs1; i1++)
            {
                for (int i0 = 0; i0 < divs0; i0++)
                {
                    int i00 = (divs0 + 1) * (i1 + 0) + (i0 + 0);
                    int i01 = (divs0 + 1) * (i1 + 0) + (i0 + 1);
                    int i10 = (divs0 + 1) * (i1 + 1) + (i0 + 0);
                    int i11 = (divs0 + 1) * (i1 + 1) + (i0 + 1);

                    int tri;

                    tri = 0;
                    indices[(i1 * divs0 + i0) * 6 + tri * 3 + 0] = i00;
                    indices[(i1 * divs0 + i0) * 6 + tri * 3 + 1] = i11;
                    indices[(i1 * divs0 + i0) * 6 + tri * 3 + 2] = i01;
                    tri = 1;
                    indices[(i1 * divs0 + i0) * 6 + tri * 3 + 0] = i00;
                    indices[(i1 * divs0 + i0) * 6 + tri * 3 + 1] = i10;
                    indices[(i1 * divs0 + i0) * 6 + tri * 3 + 2] = i11;
                }
            }

            var mesh = new Mesh();
            mesh.name = "Grid2D_" + divs0 + "x" + divs1;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            return mesh;
        }
    }

#if UNITY_EDITOR
    public partial class UnderwaterEffect : IValidated
    {
        // List of keywords shared with the ocean shader. Because finding this out dynamically is more difficult.
        static readonly List<string> sharedKeywords = new List<string>()
        {
            "_SUBSURFACESCATTERING_ON",
            "_SUBSURFACESHALLOWCOLOUR_ON",
            "_TRANSPARENCY_ON",
            "_CAUSTICS_ON",
            "_SHADOWS_ON",
        };

        public bool Validate(OceanRenderer ocean, ValidatedHelper.ShowMessage showMessage)
        {
            var isValid = true;

            // Check that underwater effect is parented to a camera.
            if (transform.parent.GetComponent<Camera>() == null)
            {
                showMessage
                (
                    "Underwater effects expect to be parented to a camera.",
                    MessageType.Error, this
                );

                isValid = false;
            }

            // Check that underwater effect has correct material assigned.
            var shaderPrefix = "Crest/Underwater";
            var renderer = GetComponent<Renderer>();
            if (renderer.sharedMaterial && renderer.sharedMaterial.shader && !renderer.sharedMaterial.shader.name.StartsWith(shaderPrefix))
            {
                showMessage
                (
                    $"Shader assigned to underwater effect expected to be of type <i>{shaderPrefix}</i>.",
                    MessageType.Error, this
                );

                isValid = false;
            }
            else if (renderer.sharedMaterial.shader.name == "Crest/Underwater Curtain")
            {
                // Check that enabled underwater material keywords are enabled on the ocean material.
                var keywords = renderer.sharedMaterial.shaderKeywords;
                foreach (var keyword in keywords)
                {
                    if (keyword == "_COMPILESHADERWITHDEBUGINFO_ON") continue;

                    if (!ocean.OceanMaterial.IsKeywordEnabled(keyword))
                    {
                        showMessage
                        (
                            $"Keyword {keyword} was enabled on the underwater material <i>{renderer.sharedMaterial.name}</i>"
                            + $"but not on the ocean material <i>{ocean.OceanMaterial.name}</i>, underwater appearance "
                            + "may not match ocean surface in standalone builds.",
                            MessageType.Warning, this
                        );
                    }
                }

                // Check that enabled ocean material keywords are enabled on the underwater material.
                keywords = ocean.OceanMaterial.shaderKeywords;
                foreach (var keyword in keywords)
                {
                    if (keyword == "_COMPILESHADERWITHDEBUGINFO_ON") continue;
                    if (!sharedKeywords.Contains(keyword)) continue;

                    if (!renderer.sharedMaterial.IsKeywordEnabled(keyword))
                    {
                        showMessage
                        (
                            $"Keyword {keyword} is enabled on the ocean material <i>{ocean.OceanMaterial.name}</i> but "
                            + $"not on the underwater material <i>{renderer.sharedMaterial.name}</i>, underwater "
                            + "appearance may not match ocean surface in standalone builds.",
                            MessageType.Warning, this
                        );
                    }
                }
            }

            return isValid;
        }
    }

    [CustomEditor(typeof(UnderwaterEffect)), CanEditMultipleObjects]
    class UnderwaterEffectEditor : ValidatedEditor { }
#endif
}
