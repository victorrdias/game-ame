using Godot;

namespace Vilanova;

public partial class HotbarUI : CanvasLayer
{
	public const int VisibleSlots = 8;

	[Export] public NodePath InventoryPath { get; set; } = new("../Player/Inventory");

	private InventoryComponent _inventory = null!;
	private Control _root = null!;
	private PanelContainer _panel = null!;
	private HBoxContainer _bar = null!;
	private InventorySlotView[] _slots = System.Array.Empty<InventorySlotView>();
	private Vector2 _laidOutFor;

	public override void _Ready()
	{
		Layer = 1;
		_inventory = GetNode<InventoryComponent>(InventoryPath);
		_root = new Control
		{
			Name = "Root",
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		_panel = new PanelContainer
		{
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		_panel.AddThemeStyleboxOverride("panel", CozyUiTheme.Panel());
		_root.AddChild(_panel);

		_bar = new HBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		_bar.AddThemeConstantOverride("separation", 6);
		_panel.AddChild(_bar);

		var count = Mathf.Min(VisibleSlots, _inventory.Capacity);
		_slots = new InventorySlotView[count];
		for (var i = 0; i < count; i++)
		{
			var slot = new InventorySlotView { MouseFilter = Control.MouseFilterEnum.Stop };
			_bar.AddChild(slot);
			_slots[i] = slot;
		}

		_inventory.InventoryChanged += Refresh;
		_root.Resized += ApplyLayout;
		CallDeferred(nameof(ApplyLayout));
		Refresh();
	}

	private void ApplyLayout()
	{
		if (_root == null || _panel == null || _slots.Length == 0)
		{
			return;
		}

		var size = _root.Size;
		if (size.X < 8f || size.Y < 8f || size == _laidOutFor)
		{
			return;
		}

		_laidOutFor = size;
		var gaps = 6f * (_slots.Length - 1);
		var slot = Mathf.Clamp((size.X * 0.46f - gaps) / _slots.Length, 44f, 58f);
		foreach (var view in _slots)
		{
			view.SetSlotSize(slot);
		}

		var width = slot * _slots.Length + gaps + CozyUiTheme.Margin * 2f;
		var height = slot + CozyUiTheme.Margin * 2f;
		_panel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
		_panel.OffsetLeft = -width * 0.5f;
		_panel.OffsetRight = width * 0.5f;
		_panel.OffsetTop = -height - 16f;
		_panel.OffsetBottom = -16f;
	}

	private void Refresh()
	{
		for (var i = 0; i < _slots.Length; i++)
		{
			var slot = _inventory.GetSlot(i);
			var definition = slot == null || slot.IsEmpty ? null : _inventory.FindDefinition(slot.ItemId);
			_slots[i].Bind(definition, slot?.Quantity ?? 0, false);
		}
	}
}
