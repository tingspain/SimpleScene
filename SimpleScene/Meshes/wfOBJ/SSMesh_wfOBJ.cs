// Copyright(C) David W. Jeske, 2013
// Released to the public domain. Use, modify and relicense at will.

using System;
using System.Collections.Generic;

using OpenTK;
using OpenTK.Graphics.OpenGL;

using Util3d;

namespace SimpleScene
{
    public class SSMesh_wfOBJ : SSAbstractMesh {
 
		protected List<SSMeshOBJSubsetData> geometrySubsets = new List<SSMeshOBJSubsetData>();
		SSAssetManagerContext ctx;
		public readonly string srcFilename;
		
		SSShaderProgram shaderPgm;

	    public struct SSMeshOBJSubsetData {
	   		public SSTexture diffuseTexture;
	   		public SSTexture specularTexture;
	   		public SSTexture ambientTexture;
	   		public SSTexture bumpTexture;			
	
			// raw geometry
			public SSVertex_PosNormDiffTex1[] vertices;
			public UInt16[] indicies;
			public UInt16[] wireframe_indicies;

			// handles to OpenTK/OpenGL Vertex-buffer and index-buffer objects
			// these are buffers stored on the videocard for higher performance rendering
			public SSVertexBuffer<SSVertex_PosNormDiffTex1> vbo;	        
			public SSIndexBuffer<UInt16> ibo;
			public SSIndexBuffer<UInt16> ibo_wireframe;
		}

		public override string ToString ()
		{
			return string.Format ("[SSMesh_FromOBJ:{0}]", this.srcFilename);
		}
		
#region Constructor
        public SSMesh_wfOBJ(SSAssetManagerContext ctx, string filename, bool mipmapped, SSShaderProgram shaderPgm = null) {
            this.srcFilename = filename;
            // this.mipmapped = mipmapped;
            this.ctx = ctx;
            this.shaderPgm = shaderPgm;


            Console.WriteLine("SSMesh_wfOBJ: loading wff {0}",filename);
            WavefrontObjLoader wff_data = new WavefrontObjLoader(filename,
               delegate(string resource_name) { return ctx.getAsset(resource_name).Open(); });


			Console.WriteLine("wff vertex count = {0}",wff_data.positions.Count);
			Console.WriteLine("wff face count = {0}",wff_data.numFaces);

            _loadData(wff_data);
        }    
#endregion

		private void _renderSetupGLSL(SSMeshOBJSubsetData subset) {
			// Step 1: setup GL rendering modes...

			GL.Enable(EnableCap.CullFace);
			// GL.Enable(EnableCap.Lighting);

			// GL.Enable(EnableCap.Blend);
			// GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
			// GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

			// Step 2: setup our material mode and paramaters...

			GL.Color3(System.Drawing.Color.White);  // clear the vertex color to white..

			if (shaderPgm == null) {
				GL.UseProgram(0);
				GL.Disable(EnableCap.CullFace);


				// fixed function single-texture
				GL.Disable(EnableCap.Texture2D);
				if (subset.diffuseTexture != null) {
					GL.ActiveTexture(TextureUnit.Texture0);
					GL.Enable(EnableCap.Texture2D);
					GL.BindTexture(TextureTarget.Texture2D, subset.diffuseTexture.TextureID);
				}
			} else {
				// activate GLSL shader
				GL.UseProgram(shaderPgm.ProgramID);

				// bind our texture-images to GL texture-units 
				// http://adriangame.blogspot.com/2010/05/glsl-multitexture-checklist.html

				// these texture-unit assignments are hard-coded in the shader setup

				GL.ActiveTexture(TextureUnit.Texture0);
				if (subset.diffuseTexture != null) {
					GL.BindTexture(TextureTarget.Texture2D, subset.diffuseTexture.TextureID);
				} else {
					GL.BindTexture(TextureTarget.Texture2D, 0);
				}
				GL.ActiveTexture(TextureUnit.Texture1);
				if (subset.specularTexture != null) {
					GL.BindTexture(TextureTarget.Texture2D, subset.specularTexture.TextureID);
				} else {
					GL.BindTexture(TextureTarget.Texture2D, 0);
				}
				GL.ActiveTexture(TextureUnit.Texture2);
				if (subset.ambientTexture != null) {
					GL.BindTexture(TextureTarget.Texture2D, subset.ambientTexture.TextureID);
				} else {
					GL.BindTexture(TextureTarget.Texture2D, 0);
				}
				GL.ActiveTexture(TextureUnit.Texture3);
				if (subset.bumpTexture != null) {
					GL.BindTexture(TextureTarget.Texture2D, subset.bumpTexture.TextureID);
				} else {
					GL.BindTexture(TextureTarget.Texture2D, 0);
				}

				// reset to texture-unit 0 to be friendly..
				GL.ActiveTexture(TextureUnit.Texture0);				
			}
		}

		private void _renderSetupWireframe() {
			GL.UseProgram(0); // turn off GLSL
			GL.Enable(EnableCap.CullFace);
			GL.Disable(EnableCap.Texture2D);
			GL.Disable(EnableCap.Blend);
			GL.Disable(EnableCap.Lighting);
		}

		public override IEnumerable<Vector3> EnumeratePoints ()
		{
		    foreach (var subset in geometrySubsets) {
				foreach (var vtx in subset.vertices) {
				    yield return vtx.Position;
				}
			}
		}
		public override bool TraverseTriangles<T>(T state, traverseFn<T> fn) {
			foreach(var subset in geometrySubsets) {
				for(int idx=0;idx < subset.indicies.Length;idx+=3) {
					var V1 = subset.vertices[subset.indicies[idx]].Position;
					var V2 = subset.vertices[subset.indicies[idx+1]].Position;
					var V3 = subset.vertices[subset.indicies[idx+2]].Position;
					bool finished = fn(state, V1, V2, V3);
					if (finished) { 
						return true; 
					}
				}
			}
			return false;
		}



		private void _renderSendVBOTriangles(SSMeshOBJSubsetData subset) {
			subset.vbo.bind (this.shaderPgm);
			subset.ibo.bind ();

			//GL.DrawArrays (PrimitiveType.Triangles, 0, 6);
			GL.DrawElements (PrimitiveType.Triangles, subset.indicies.Length, DrawElementsType.UnsignedShort, IntPtr.Zero);
			subset.ibo.unbind ();
			subset.vbo.unbind ();
		}
		private void _renderSendTriangles(SSMeshOBJSubsetData subset) {
			// Step 3: draw faces.. here we use the "old school" manual method of drawing

			//         note: programs written for modern OpenGL & D3D don't do this!
			//               instead, they hand the vertex-buffer and index-buffer to the
			//               GPU and let it do this..		

			GL.Begin(BeginMode.Triangles);
			foreach(var idx in subset.indicies) {
				var vertex = subset.vertices[idx];  // retrieve the vertex

				// draw the vertex..
				GL.Color3(System.Drawing.Color.FromArgb(vertex.DiffuseColor));
				GL.TexCoord2(vertex.Tu,vertex.Tv);
				GL.Normal3(vertex.Normal);
				GL.Vertex3(vertex.Position);
			}
			GL.End();
		}
			
		private void _renderSendVBOLines(SSMeshOBJSubsetData subset) {
			// TODO: this currently has classic problems with z-fighting between the model and the wireframe
			//     it is customary to "bump" the wireframe slightly towards the camera to prevent this. 
			subset.vbo.bind (null);
			subset.ibo_wireframe.bind ();
			GL.LineWidth (1.5f);
			GL.Color4 (0.8f, 0.5f, 0.5f, 0.5f);		
			GL.DrawElements (PrimitiveType.Lines, subset.wireframe_indicies.Length, DrawElementsType.UnsignedShort, IntPtr.Zero);
			subset.ibo.unbind ();
			subset.vbo.unbind ();

		}
		private void _renderSendLines(SSMeshOBJSubsetData subset) {
			// Step 3: draw faces.. here we use the "old school" manual method of drawing

			//         note: programs written for modern OpenGL & D3D don't do this!
			//               instead, they hand the vertex-buffer and index-buffer to the
			//               GPU and let it do this..


			for(int i=2;i<subset.indicies.Length;i+=3) {
				var v1 = subset.vertices [subset.indicies[i - 2]];
				var v2 = subset.vertices [subset.indicies[i - 1]];
				var v3 = subset.vertices [subset.indicies[i]];

				// draw the vertex..
				GL.Color3(System.Drawing.Color.FromArgb(v1.DiffuseColor));

				GL.Begin(BeginMode.LineLoop);
				GL.Vertex3 (v1.Position);
				GL.Vertex3 (v2.Position);
				GL.Vertex3 (v3.Position);
				GL.End();
			}

		}


		public override void RenderMesh(ref SSRenderConfig renderConfig) {		
			foreach (SSMeshOBJSubsetData subset in this.geometrySubsets) {

				if (renderConfig.drawGLSL) {
					_renderSetupGLSL (subset);
					if (renderConfig.useVBO && shaderPgm != null) {
						_renderSendVBOTriangles (subset);
					} else {
						_renderSendTriangles (subset);
					}
			
				}

				if (renderConfig.drawWireframeMode == WireframeMode.GL_Lines) {
					_renderSetupWireframe ();
					if (renderConfig.useVBO && shaderPgm != null) {
						_renderSendVBOLines (subset);
					} else {
						_renderSendLines (subset);
					}
				}
			}
		}

#region Load Data
        private void _loadData(WavefrontObjLoader m) {
            foreach (var srcmat in m.materials) {
                if (srcmat.faces.Count != 0) {
                    this.geometrySubsets.Add(_loadMaterialSubset(m, srcmat));
                }
            }
        }
        
        private SSMeshOBJSubsetData _loadMaterialSubset(WavefrontObjLoader wff, WavefrontObjLoader.MaterialFromObj objMatSubset) {
            // create new mesh subset-data
            SSMeshOBJSubsetData subsetData = new SSMeshOBJSubsetData();            

            // setup the material...            

            // load and link every texture present 
            if (objMatSubset.diffuseTextureResourceName != null) {
                subsetData.diffuseTexture = new SSTexture_FromAsset(ctx.getAsset(objMatSubset.diffuseTextureResourceName));
            }
            if (objMatSubset.ambientTextureResourceName != null) {
                subsetData.ambientTexture = new SSTexture_FromAsset(ctx.getAsset(objMatSubset.ambientTextureResourceName));
            } 
            if (objMatSubset.bumpTextureResourceName != null) {
                subsetData.bumpTexture = new SSTexture_FromAsset(ctx.getAsset(objMatSubset.bumpTextureResourceName));
            }
            if (objMatSubset.specularTextureResourceName != null) {
                subsetData.specularTexture = new SSTexture_FromAsset(ctx.getAsset(objMatSubset.specularTextureResourceName));
            }

            // generate renderable geometry data...
            VertexSoup_VertexFormatBinder.generateDrawIndexBuffer(wff, out subsetData.indicies, out subsetData.vertices);           

			// TODO: setup VBO/IBO buffers
			// http://www.opentk.com/doc/graphics/geometry/vertex-buffer-objects

			subsetData.wireframe_indicies = OpenTKHelper.generateLineIndicies (subsetData.indicies);

			subsetData.vbo = new SSVertexBuffer<SSVertex_PosNormDiffTex1>(subsetData.vertices);
			subsetData.ibo = new SSIndexBuffer<UInt16> (subsetData.indicies, sizeof(UInt16));		
			subsetData.ibo_wireframe = new SSIndexBuffer<UInt16> (subsetData.wireframe_indicies, sizeof(UInt16));		

            return subsetData;
        }
#endregion
    }
}