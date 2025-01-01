using Content.Client.UserInterface.Controls;
using Content.Shared._DV.VendingMachines;
using Content.Shared.Stacks;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Client._DV.VendingMachines.UI;

[GenerateTypedNameReferences]
public sealed partial class ShopVendorWindow : FancyWindow
{
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    private readonly ShopVendorSystem _vendor;

    /// <summary>
    /// Event fired with the listing index to purchase.
    /// </summary>
    public event Action<int>? OnItemSelected;

    private EntityUid _owner;
    private readonly StyleBoxFlat _style = new() { BackgroundColor = new Color(70, 73, 102) };
    private readonly StyleBoxFlat _styleBroke = new() { BackgroundColor = Color.FromHex("#303133") };
    private readonly List<ListContainerButton> _buttons = new();
    private uint _balance = 1;

    public ShopVendorWindow()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        _vendor = _entMan.System<ShopVendorSystem>();

        VendingContents.SearchBar = SearchBar;
        VendingContents.DataFilterCondition += DataFilterCondition;
        VendingContents.GenerateItem += GenerateButton;
        VendingContents.ItemKeyBindDown += (args, data) => OnItemSelected?.Invoke(((ShopVendorListingData) data).Index);
    }

    public void SetEntity(EntityUid owner)
    {
        _owner = owner;

        if (!_entMan.TryGetComponent<ShopVendorComponent>(owner, out var comp))
            return;

        var pack = _proto.Index(comp.Pack);
        Populate(pack.Listings);

        UpdateBalance();
    }

    private void UpdateBalance(uint balance)
    {
        if (_balance == balance)
            return;

        _balance = balance;

        BalanceLabel.Text = Loc.GetString("shop-vendor-balance", ("points", balance));

        // disable items that are too expensive to buy
        foreach (var button in _buttons)
        {
            if (button.Data is ShopVendorListingData data)
                button.Disabled = data.Cost > balance;

            button.StyleBoxOverride = button.Disabled ? _styleBroke : _style;
        }
    }

    private void UpdateBalance()
    {
        if (_player.LocalEntity is {} user)
            UpdateBalance(_vendor.GetBalance(_owner, user));
    }

    private bool DataFilterCondition(string filter, ListData data)
    {
        if (data is not ShopVendorListingData { Text: var text })
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        return text.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
    }

    private void GenerateButton(ListData data, ListContainerButton button)
    {
        if (data is not ShopVendorListingData cast)
            return;

        _buttons.Add(button);
        button.AddChild(new ShopVendorItem(cast.ItemId, cast.Text, cast.Cost));

        button.ToolTip = cast.Text;
        button.Disabled = cast.Cost > _balance;
        button.StyleBoxOverride = button.Disabled ? _styleBroke : _style;
    }

    public void Populate(List<ShopListing> listings)
    {
        var longestEntry = string.Empty;
        var listData = new List<ShopVendorListingData>();
        for (var i = 0; i < listings.Count; i++)
        {
            var listing = listings[i];
            var proto = _proto.Index(listing.Id);
            var text = proto.Name;
            if (proto.TryGetComponent<StackComponent>(out var stack, _factory) && stack.Count > 1)
            {
                text += " ";
                text += Loc.GetString("shop-vendor-stack-suffix", ("count", stack.Count));
            }
            listData.Add(new ShopVendorListingData(i, listing.Id, text, listing.Cost));
        }

        _buttons.Clear();
        VendingContents.PopulateList(listData);
        SetSizeAfterUpdate(longestEntry.Length, listings.Count);
    }

    private void SetSizeAfterUpdate(int longestEntryLength, int contentCount)
    {
        SetSize = new Vector2(Math.Clamp((longestEntryLength + 2) * 12, 250, 400),
            Math.Clamp(contentCount * 50, 150, 350));
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        UpdateBalance();
    }
}

public record ShopVendorListingData(int Index, EntProtoId ItemId, string Text, uint Cost) : ListData;
