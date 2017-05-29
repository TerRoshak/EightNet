using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Windows.Forms;
using System.IO;
using EightNet.Chip8;

namespace EightNet
{
    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class Game1 : Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        Texture2D frameBuffer;

        CPU chip8;

        DebugForm dbg;

        bool[] Keys = new bool[16];
        
        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            graphics.PreferredBackBufferWidth = 640;
            graphics.PreferredBackBufferHeight = 320;
            graphics.ApplyChanges();

            Window.AllowUserResizing = true;

            for (int i = 0; i < 16; i++) Keys[i] = false;

            chip8 = new CPU();
            dbg = new DebugForm(chip8);
            dbg.Show();
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            frameBuffer = new Texture2D(GraphicsDevice, 64, 32);

            // loading a rom and starting emulation
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.DefaultExt = ".c8";
            ofd.Filter = "ROM files (.c8)|*.c8|*.*|*.*";
            ofd.Multiselect = false;

            System.Windows.Forms.DialogResult result = ofd.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                string filename = ofd.FileName;

                using (FileStream fs = new FileStream(filename, FileMode.Open))
                {
                    using (BinaryReader br = new BinaryReader(fs))
                    {
                        byte[] rom = new byte[fs.Length];
                        for (int i = 0; i < fs.Length; i++)
                            rom[i] = br.ReadByte();
                        chip8.load(rom);
                    }
                }

                // TODO: use this.Content to load your game content here
                chip8.start();
            }

            

        }


        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// game-specific content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
            chip8.stop();
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == Microsoft.Xna.Framework.Input.ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
                Exit();

            /*
             * Original Layout :
             * 1 2 3 C
             * 4 5 6 D
             * 7 8 9 E
             * A 0 B F
             */

            Keys[0x1] = Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.D1);
            Keys[0x2] = Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.D2);
            Keys[0x3] = Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.D3);
            Keys[0xC] = Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.D4);

            Keys[0x4] = Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Q);
            Keys[0x5] = Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.W);
            Keys[0x6] = Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.E);
            Keys[0xD] = Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.R);

            Keys[0x7] = Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.A);
            Keys[0x8] = Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.S);
            Keys[0x9] = Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.D);
            Keys[0xE] = Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F);

            Keys[0xA] = Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Y);
            Keys[0x0] = Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.X);
            Keys[0xB] = Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.C);
            Keys[0xF] = Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.V);

            chip8.setKeys(Keys);

            // TODO: Add your update logic here
            byte[] fb = chip8.getFB();

            //expand framebuffer
            byte[] fb_exp = new byte[fb.Length * 8 * 4];
            for(int i=0;i<(fb.Length*8);i++)
            {
                bool c = ((fb[i / 8] & (0x80 >> (i % 8))) > 0);
                byte col = 0;
                if (c) col = 0xFF;
                fb_exp[i * 4]     = col;
                fb_exp[(i * 4)+1] = col;
                fb_exp[(i * 4)+2] = col;
                fb_exp[(i * 4)+3] = col;
            }

            frameBuffer.SetData<byte>(fb_exp);

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            // TODO: Add your drawing code here
            Rectangle bounds = GraphicsDevice.Viewport.Bounds;

            float aspectRatio = GraphicsDevice.Viewport.Bounds.Width / (float)GraphicsDevice.Viewport.Bounds.Height;
            float targetAspectRatio = 64.0f / 32.0f;

            if (aspectRatio > targetAspectRatio)
            {
                int targetWidth = (int)(bounds.Height * targetAspectRatio);
                bounds.X = (bounds.Width - targetWidth) / 2;
                bounds.Width = targetWidth;
            }
            else if (aspectRatio < targetAspectRatio)
            {
                int targetHeight = (int)(bounds.Width / targetAspectRatio);
                bounds.Y = (bounds.Height - targetHeight) / 2;
                bounds.Height = targetHeight;
            }

            // draw backbuffer
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(frameBuffer, bounds, Color.White);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
