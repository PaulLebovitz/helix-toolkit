﻿/*
The MIT License (MIT)
Copyright (c) 2018 Helix Toolkit contributors
*/
#if !NETFX_CORE
namespace HelixToolkit.Wpf.SharpDX.Core
#else
namespace HelixToolkit.UWP.Core
#endif
{
    using Shaders;
    using Render;
    using global::SharpDX.Direct3D11;
    using global::SharpDX;
    using Utilities;
    using Model;

    public class MeshRenderCore : GeometryRenderCore<ModelStruct>, IMeshRenderParams, IDynamicReflectable
    {
        #region Variables
        /// <summary>
        /// Gets the raster state wireframe.
        /// </summary>
        /// <value>
        /// The raster state wireframe.
        /// </value>
        protected RasterizerStateProxy RasterStateWireframe { get { return rasterStateWireframe; } }
        private RasterizerStateProxy rasterStateWireframe = null;

        #endregion

        #region Properties
        /// <summary>
        /// 
        /// </summary>
        public bool InvertNormal
        {
            set
            {
                SetAffectsRender(ref modelStruct.InvertNormal, (value ? 1 : 0));
            }
            get
            {
                return modelStruct.InvertNormal == 1 ? true : false;
            }
        }
        private bool renderWireframe = false;
        /// <summary>
        /// Gets or sets a value indicating whether [render wireframe].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [render wireframe]; otherwise, <c>false</c>.
        /// </value>
        public bool RenderWireframe
        {
            set
            {
                SetAffectsRender(ref renderWireframe, value);
            }
            get
            {
                return renderWireframe;
            }
        }

        /// <summary>
        /// Gets or sets the color of the wireframe.
        /// </summary>
        /// <value>
        /// The color of the wireframe.
        /// </value>
        public Color4 WireframeColor
        {
            set
            {
                SetAffectsRender(ref modelStruct.WireframeColor, value);
            }
            get { return modelStruct.WireframeColor; }
        }



        /// <summary>
        /// Gets or sets the dynamic reflector.
        /// </summary>
        /// <value>
        /// The dynamic reflector.
        /// </value>
        public IDynamicReflector DynamicReflector
        {
            set; get;
        }
        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="MeshRenderCore"/> is batched.
        /// </summary>
        /// <value>
        ///   <c>true</c> if batched; otherwise, <c>false</c>.
        /// </value>
        public bool Batched
        {
            set; get;
        } = false;

        private MaterialVariable materialVariables = EmptyMaterialVariable.EmptyVariable;
        /// <summary>
        /// Used to wrap all material resources
        /// </summary>
        public MaterialVariable MaterialVariables
        {
            set
            {
                var old = materialVariables;
                if (Set(ref materialVariables, value))
                {
                    if (value == null)
                    {
                        materialVariables = EmptyMaterialVariable.EmptyVariable;
                    }
                }
            }
            get
            {
                return materialVariables;
            }
        }
        #endregion

        protected override bool CreateRasterState(RasterizerStateDescription description, bool force)
        {
            if (base.CreateRasterState(description, force))
            {
                RemoveAndDispose(ref rasterStateWireframe);
                var wireframeDesc = description;
                wireframeDesc.FillMode = FillMode.Wireframe;
                wireframeDesc.DepthBias = -100;
                wireframeDesc.SlopeScaledDepthBias = -2f;
                wireframeDesc.DepthBiasClamp = -0.00008f;
                rasterStateWireframe = Collect(EffectTechnique.EffectsManager.StateManager.Register(wireframeDesc));
                return true;
            }
            else
            {
                return false;
            }
        }

        protected override void OnDetach()
        {
            DynamicReflector = null;
            rasterStateWireframe = null;
            base.OnDetach();
        }

        protected override void OnUpdatePerModelStruct(ref ModelStruct model, RenderContext context)
        {
            model.World = ModelMatrix;
            model.HasInstances = InstanceBuffer == null ? 0 : InstanceBuffer.HasElements ? 1 : 0;
            model.RenderOIT = context.IsOITPass ? 1 : 0;
            model.Batched = Batched ? 1 : 0;
        }

        protected override void OnRender(RenderContext context, DeviceContextProxy deviceContext)
        {
            ShaderPass pass = MaterialVariables.GetPass(RenderType, context);
            if (pass.IsNULL)
            { return; }
            MaterialVariables.UpdateMaterialStruct(deviceContext, ref modelStruct);
            pass.BindShader(deviceContext);
            pass.BindStates(deviceContext, DefaultStateBinding);
            if (!materialVariables.BindMaterial(context, deviceContext, pass))
            {
                return;
            }

            DynamicReflector?.BindCubeMap(deviceContext);
            materialVariables.Draw(deviceContext, GeometryBuffer.IndexBuffer, InstanceBuffer);
            DynamicReflector?.UnBindCubeMap(deviceContext);

            if (RenderWireframe)
            {
                pass = materialVariables.GetWireframePass(RenderType, context);
                if (pass.IsNULL)
                {
                    return;
                }
                pass.BindShader(deviceContext, false);
                pass.BindStates(deviceContext, DefaultStateBinding);
                deviceContext.SetRasterState(RasterStateWireframe);
                materialVariables.Draw(deviceContext, GeometryBuffer.IndexBuffer, InstanceBuffer);
            }
        }

        protected override void OnRenderCustom(RenderContext context, DeviceContextProxy deviceContext)
        {
            MaterialVariables.UpdateMaterialStruct(deviceContext, ref modelStruct);
            materialVariables.Draw(deviceContext, GeometryBuffer.IndexBuffer, InstanceBuffer);
        }

        protected override void OnRenderShadow(RenderContext context, DeviceContextProxy deviceContext)
        {
            var pass = materialVariables.GetShadowPass(RenderType, context);
            if (!IsThrowingShadow || pass.IsNULL)
            { return; }
            MaterialVariables.UpdateModelStructOnly(deviceContext, ref modelStruct);
            pass.BindShader(deviceContext);
            pass.BindStates(deviceContext, ShadowStateBinding);
            materialVariables.Draw(deviceContext, GeometryBuffer.IndexBuffer, InstanceBuffer);
        }
    }
}
