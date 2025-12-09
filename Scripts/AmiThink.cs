using Godot;
using System;
using OpenAmi.Scripts;


namespace OpenAmi.Scripts
{
    public partial class AmiThink : Control
    {
        public TextureRect _pic;
        public AnimationPlayer _anim;

        public override void _Ready()
        {
            _pic = GetNode<TextureRect>("PanelContainer/Pic");
            _anim = GetNode<AnimationPlayer>("Anim");
        }

    }
}
