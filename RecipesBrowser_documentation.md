# Project Documentation: RecipesBrowser
## 1. README
```markdown
# RecipesBrowser 合成表

- 作者: 棱镜、羽学、Cai
- 出处: [github](https://github.com/1242509682/RecipesBrowser)
- 由于PE的Terraria的向导存在一些恶性bug (远古时期)  
- 导致在大多数服务器中向导被禁用，这样一来想要查合成表就非常麻烦，  
- 所以写了这样一个插件，支持查找“此物品的配方”和“此物品可以合成什么”

## 指令

| 语法        |       权限       |            说明            |
|-----------|:--------------:|:------------------------:|
| /fd、/find | RecipesBrowser | /fd <物品ID> 查询合成所需材料与工作站  |
| /查        | RecipesBrowser | /fd <物品ID> r 查询该材料可合成的物品 |

## 配置

```json
暂无
```

## 更新日志

### v1.1.1
- 修复无参数报错问题
- 优化代码
### v1.0.6
- 完善卸载函数
### v0.5
- 修复未释放钩子导致关闭服务器时的报错
### v0.4
- 适配.NET 6.0
- 添加中文命令
- 添加一个权限名

## 反馈
- 优先发issued -> 共同维护的插件库：https://github.com/UnrealMultiple/TShockPlugin
- 次优先：TShock官方群：816771079
- 大概率看不到但是也可以：国内社区trhub.cn ，bbstr.net , tr.monika.love```
## 3. Project File Tree
```
temp_RecipesBrowser
├── README.en-US.md
├── README.md
├── RecipesBrowser.cs
├── RecipesBrowser.csproj
├── i18n
│   ├── en-US.po
│   ├── es-ES.po
│   ├── ru-RU.po
│   └── template.pot
└── manifest.json

2 directories, 9 files
```
## 4. Source Files
### Folder: `temp_RecipesBrowser`
#### File: `temp_RecipesBrowser/README.en-US.md`
```
# RecipesBrowser recipes table

- author: 棱镜、羽学
- source: [github](https://github.com/1242509682/RecipesBrowser)
- due to some vicious bugs in the guide of PE Terraria (ancient times)
- as a result, the wizard is disabled in most servers, which makes it very troublesome to look up the table.
- so I wrote a plug-in that supports searching for "the recipe of this item" and "what this item can be synthesized into."

## Instruction

| Command        |       Permissions       |            Description            |
|-----------|:--------------:|:------------------------:|
| /fd、/find | RecipesBrowser | /fd <Item ID> Query the materials and workstations required for synthesis  |
| /查        | RecipesBrowser | /fd <Item ID> Query the items that can be synthesized from this material |

## Configuration

```json
none
```

## Change log

```
- 1.0.6
- Improve the uninstall function
- 0.5
- Fixed an error when shutting down the server due to unreleased hooks
- 0.4
- Adapted to .net 6.0
- Added Chinese commands and added a permission name
```

## Feedback
- Github Issue -> TShockPlugin Repo: https://github.com/UnrealMultiple/TShockPlugin
- TShock QQ Group: 816771079
- China Terraria Forum: trhub.cn, bbstr.net, tr.monika.love

```
#### File: `temp_RecipesBrowser/RecipesBrowser.cs`
```
﻿using Microsoft.Xna.Framework;
using MonoMod.Cil;
using System.Text;
using Terraria;
using Terraria.Map;
using TerrariaApi.Server;
using TShockAPI;

[ApiVersion(2, 1)]
public class Plugin : TerrariaPlugin
{
    public override string Name => "RecipesBrowser";
    public override Version Version => new Version(1, 1, 3);

    public override string Author => "棱镜,羽学适配,Cai优化";

    public override string Description => GetString("通过指令获取物品合成表");

    public Plugin(Main game)
        : base(game)
    {
    }

    public override void Initialize()
    {
        Commands.ChatCommands.Add(new Command("RecipesBrowser", this.FindRecipe, "find", "fd", "查"));
        IL.Terraria.Lang.BuildMapAtlas += this.LangOnBuildMapAtlas;
        MapHelper.Initialize();
        Lang.BuildMapAtlas();
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Commands.ChatCommands.RemoveAll(x => x.CommandDelegate == this.FindRecipe);
            IL.Terraria.Lang.BuildMapAtlas -= this.LangOnBuildMapAtlas;
        }
        base.Dispose(disposing);
    }

    private void LangOnBuildMapAtlas(ILContext il)
    {
        il.Instrs.RemoveAt(0);
        il.Instrs.RemoveAt(0);
        il.Instrs.RemoveAt(0);
    }

    private void FindRecipe(CommandArgs args)
    {
        if (args.Parameters.Count==0)
        {
            args.Player.SendErrorMessage(GetString("格式错误!正确格式: /find <物品ID|物品名>"));
            return;
        }
        
        var itemByIdOrName = TShock.Utils.GetItemByIdOrName(args.Parameters[0]);
        
        if (itemByIdOrName.Count == 0)
        {
            args.Player.SendErrorMessage(GetString("未找到物品"));
            return;
        }
        
        if (itemByIdOrName.Count > 1)
        {
            args.Player.SendMultipleMatchError(itemByIdOrName.Select(x=> $"{x.Name}({x.type})"));
            return;
        }
        
        var item = itemByIdOrName[0];
        var mode = args.Parameters.Count > 1 ? args.Parameters[1] : "c";
        if (mode.ToLower() == "r")
        {
            args.Player.SendSuccessMessage(this.GetRecipeStringByRequired(item));
            return;
        }

        var list = Main.recipe.ToList().FindAll(r => r.createItem.type == item.type);
        var result = new StringBuilder();
        result.AppendLine(GetString($"物品:{TShock.Utils.ItemTag(item)}"));
        if (list.Count == 0)
        {
            args.Player.SendErrorMessage(GetString("此物品无配方"));
            return;
        }
        if (list.Count >= 1)
        {
            
            for (var i = 0; i < list.Count; i++)
            {
                var numberIcons = i.ToString()
                    .Select(x => 2703 +int.Parse(x.ToString()))
                    .Select(x=> $"[i:{x}]");
                
                result.AppendLine(GetString($"{string.Join("",numberIcons)}配方{i + 1}:"));
                result.AppendLine(GetRecipeStringByResult(list[i]));
            }
        }
        args.Player.SendWarningMessage(result.ToString().Trim('\n'));
    }
    


    private static string GetRecipeStringByResult(Recipe recipe)
    {
        
        var result = new StringBuilder();
        result.Append(GetString("材料："));
        foreach (var item in recipe.requiredItem.Where(r => r.stack > 0))
        {
            result.Append($"{TShock.Utils.ItemTag(item)}{item.Name}{(item.maxStack == 1 || item.stack == 0 ? "" : "x" + item.stack)} ");
        }
        result.AppendLine();
        
        result.Append(GetString("合成站："));
        foreach (var item2 in recipe.requiredTile.Where(i => i >= 0))
        {
            result.Append($"{Lang._mapLegendCache[MapHelper.tileLookup[item2]]} ");
        }
        if (recipe.requiredTile.Length==0)
        {
            result.AppendLine(GetString("[i:3258]徒手 "));
        }
        if (recipe.needHoney)
        {
            result.AppendLine(GetString("[i:1134]蜂蜜 "));
        }
        if (recipe.needWater)
        {
            result.AppendLine(GetString("[i:126]水 "));
        }
        if (recipe.needLava)
        {
            result.AppendLine(GetString("[i:4825]岩浆 "));
        }

        if (recipe.needSnowBiome)
        {
            result.AppendLine(GetString("[i:593]雪原群系 "));
        }
        if (recipe.needEverythingSeed)
        {
            result.AppendLine(GetString("[i:4956]天顶种子 "));
        }

        if (recipe.needGraveyardBiome)
        {
            result.AppendLine(GetString("[i:321]墓地 "));
        }
        return result.ToString().Trim('\n');
    }

    private string GetRecipeStringByRequired(Item item)
    {
        var result = new StringBuilder();
        result.AppendLine(GetString("可合成的物品:\n"));
        var source = Main.recipe.Where(r => r.requiredItem.Select(i => i.type).Contains(item.type)).Select(r => r.createItem).ToArray();
        for (var j = 1; j <= source.Length; j++)
        {
            var sourceItem = source.ElementAt(j - 1);
            result.Append($"{TShock.Utils.ItemTag(sourceItem)}{sourceItem.Name}{(sourceItem.maxStack > 1 ? "x" + sourceItem.stack : "")},{(j % 5 == 0 ? "\n" : "")}");
        }
        return result.ToString().Trim(',').Trim('\n');
    }
}
```
#### File: `temp_RecipesBrowser/RecipesBrowser.csproj`
```
<Project Sdk="Microsoft.NET.Sdk">
	<Import Project="..\..\template.targets" />
</Project>
```
### Folder: `temp_RecipesBrowser/i18n`
#### File: `temp_RecipesBrowser/i18n/en-US.po`
```
msgid ""
msgstr ""
"Project-Id-Version: tshock-chinese-plugin\n"
"POT-Creation-Date: 2025-01-11 17:28:21+0000\n"
"PO-Revision-Date: 2025-01-13 01:13\n"
"Last-Translator: \n"
"Language-Team: English\n"
"MIME-Version: 1.0\n"
"Content-Type: text/plain; charset=UTF-8\n"
"Content-Transfer-Encoding: 8bit\n"
"X-Generator: GetText.NET Extractor\n"
"Plural-Forms: nplurals=2; plural=(n != 1);\n"
"X-Crowdin-Project: tshock-chinese-plugin\n"
"X-Crowdin-Project-ID: 751499\n"
"X-Crowdin-Language: en\n"
"X-Crowdin-File: /master/src/RecipesBrowser/i18n/template.pot\n"
"X-Crowdin-File-ID: 1510\n"
"Language: en_US\n"

#: ../../RecipesBrowser.cs:127
msgid "[i:1134]蜂蜜 "
msgstr "[i:1134] Honey "

#: ../../RecipesBrowser.cs:131
msgid "[i:126]水 "
msgstr "[i:126] Water "

#: ../../RecipesBrowser.cs:149
msgid "[i:321]墓地 "
msgstr "[i:321] Ecto Mist "

#: ../../RecipesBrowser.cs:123
msgid "[i:3258]徒手 "
msgstr "[i:3258] Slap Hand "

#: ../../RecipesBrowser.cs:135
msgid "[i:4825]岩浆 "
msgstr "[i:4825] Lava "

#: ../../RecipesBrowser.cs:144
msgid "[i:4956]天顶种子 "
msgstr "[i:4956] Zenith Seeds "

#: ../../RecipesBrowser.cs:140
msgid "[i:593]雪原群系 "
msgstr "[i:593] Snow Biome "

#: ../../RecipesBrowser.cs:96
#, csharp-format
msgid "{0}配方{1}:"
msgstr "{0}Recipe{1}:"

#: ../../RecipesBrowser.cs:157
msgid "可合成的物品:\n"
msgstr "Items can be crafted:\n"

#: ../../RecipesBrowser.cs:116
msgid "合成站："
msgstr "Crafting Station:"

#: ../../RecipesBrowser.cs:61
msgid "未找到物品"
msgstr "Item not found"

#: ../../RecipesBrowser.cs:109
msgid "材料："
msgstr "Material:"

#: ../../RecipesBrowser.cs:53
msgid "格式错误!正确格式: /find <物品ID|物品名>"
msgstr "Invalid syntax! Correct syntax: /find <item ID|item name>"

#: ../../RecipesBrowser.cs:84
msgid "此物品无配方"
msgstr "There is no recipe for this item"

#: ../../RecipesBrowser.cs:81
#, csharp-format
msgid "物品:{0}"
msgstr "Item:{0}"

#: ../../RecipesBrowser.cs:17
msgid "通过指令获取物品合成表"
msgstr "Ask item recipes with commands"


```
#### File: `temp_RecipesBrowser/i18n/es-ES.po`
```
msgid ""
msgstr ""
"Project-Id-Version: tshock-chinese-plugin\n"
"POT-Creation-Date: 2025-01-11 17:28:21+0000\n"
"PO-Revision-Date: 2025-01-12 01:16\n"
"Last-Translator: \n"
"Language-Team: Spanish\n"
"MIME-Version: 1.0\n"
"Content-Type: text/plain; charset=UTF-8\n"
"Content-Transfer-Encoding: 8bit\n"
"X-Generator: GetText.NET Extractor\n"
"Plural-Forms: nplurals=2; plural=(n != 1);\n"
"X-Crowdin-Project: tshock-chinese-plugin\n"
"X-Crowdin-Project-ID: 751499\n"
"X-Crowdin-Language: es-ES\n"
"X-Crowdin-File: /master/src/RecipesBrowser/i18n/template.pot\n"
"X-Crowdin-File-ID: 1510\n"
"Language: es_ES\n"

#: ../../RecipesBrowser.cs:127
msgid "[i:1134]蜂蜜 "
msgstr ""

#: ../../RecipesBrowser.cs:131
msgid "[i:126]水 "
msgstr ""

#: ../../RecipesBrowser.cs:149
msgid "[i:321]墓地 "
msgstr ""

#: ../../RecipesBrowser.cs:123
msgid "[i:3258]徒手 "
msgstr ""

#: ../../RecipesBrowser.cs:135
msgid "[i:4825]岩浆 "
msgstr ""

#: ../../RecipesBrowser.cs:144
msgid "[i:4956]天顶种子 "
msgstr ""

#: ../../RecipesBrowser.cs:140
msgid "[i:593]雪原群系 "
msgstr ""

#: ../../RecipesBrowser.cs:96
#, csharp-format
msgid "{0}配方{1}:"
msgstr ""

#: ../../RecipesBrowser.cs:157
msgid "可合成的物品:\n"
msgstr ""

#: ../../RecipesBrowser.cs:116
msgid "合成站："
msgstr ""

#: ../../RecipesBrowser.cs:61
msgid "未找到物品"
msgstr ""

#: ../../RecipesBrowser.cs:109
msgid "材料："
msgstr ""

#: ../../RecipesBrowser.cs:53
msgid "格式错误!正确格式: /find <物品ID|物品名>"
msgstr ""

#: ../../RecipesBrowser.cs:84
msgid "此物品无配方"
msgstr ""

#: ../../RecipesBrowser.cs:81
#, csharp-format
msgid "物品:{0}"
msgstr ""

#: ../../RecipesBrowser.cs:17
msgid "通过指令获取物品合成表"
msgstr ""


```
#### File: `temp_RecipesBrowser/i18n/ru-RU.po`
```
msgid ""
msgstr ""
"Project-Id-Version: tshock-chinese-plugin\n"
"POT-Creation-Date: 2025-01-11 17:28:21+0000\n"
"PO-Revision-Date: 2025-04-13 02:42\n"
"Last-Translator: \n"
"Language-Team: Russian\n"
"MIME-Version: 1.0\n"
"Content-Type: text/plain; charset=UTF-8\n"
"Content-Transfer-Encoding: 8bit\n"
"X-Generator: GetText.NET Extractor\n"
"Plural-Forms: nplurals=4; plural=((n%10==1 && n%100!=11) ? 0 : ((n%10 >= 2 && n%10 <=4 && (n%100 < 12 || n%100 > 14)) ? 1 : ((n%10 == 0 || (n%10 >= 5 && n%10 <=9)) || (n%100 >= 11 && n%100 <= 14)) ? 2 : 3));\n"
"X-Crowdin-Project: tshock-chinese-plugin\n"
"X-Crowdin-Project-ID: 751499\n"
"X-Crowdin-Language: ru\n"
"X-Crowdin-File: /master/src/RecipesBrowser/i18n/template.pot\n"
"X-Crowdin-File-ID: 1510\n"
"Language: ru_RU\n"

#: ../../RecipesBrowser.cs:127
msgid "[i:1134]蜂蜜 "
msgstr "[i:1134]Мёд (лужа мёда)"

#: ../../RecipesBrowser.cs:131
msgid "[i:126]水 "
msgstr "[i:126]Вода (лужа воды) | [i:2827) любая раковина"

#: ../../RecipesBrowser.cs:149
msgid "[i:321]墓地 "
msgstr "[i:321]Мини-биом кладбища"

#: ../../RecipesBrowser.cs:123
msgid "[i:3258]徒手 "
msgstr "[i:3258]Не требуется"

#: ../../RecipesBrowser.cs:135
msgid "[i:4825]岩浆 "
msgstr "[i:4825]Лава (лужа лавы)"

#: ../../RecipesBrowser.cs:144
msgid "[i:4956]天顶种子 "
msgstr "[i:4956]Только на сиде getfixedboi !"

#: ../../RecipesBrowser.cs:140
msgid "[i:593]雪原群系 "
msgstr "[i:593]Снежный биом"

#: ../../RecipesBrowser.cs:96
#, csharp-format
msgid "{0}配方{1}:"
msgstr "{0}Репецт-{1}:"

#: ../../RecipesBrowser.cs:157
msgid "可合成的物品:\n"
msgstr "Возможные крафты:\n"

#: ../../RecipesBrowser.cs:116
msgid "合成站："
msgstr "На чём крафтить: "

#: ../../RecipesBrowser.cs:61
msgid "未找到物品"
msgstr "Предмет не найден!"

#: ../../RecipesBrowser.cs:109
msgid "材料："
msgstr "Материалы: "

#: ../../RecipesBrowser.cs:53
msgid "格式错误!正确格式: /find <物品ID|物品名>"
msgstr "Неверно введена команда! Править: /find <item ID|item name>"

#: ../../RecipesBrowser.cs:84
msgid "此物品无配方"
msgstr "Нет рецептов для этого предмета"

#: ../../RecipesBrowser.cs:81
#, csharp-format
msgid "物品:{0}"
msgstr "Предмет: {0}"

#: ../../RecipesBrowser.cs:17
msgid "通过指令获取物品合成表"
msgstr "Позволяет узнать рецепт предмета командой."


```
#### File: `temp_RecipesBrowser/i18n/template.pot`
```
msgid ""
msgstr ""
"Project-Id-Version: RecipesBrowser\n"
"POT-Creation-Date: 2025-01-11 17:28:21+0000\n"
"PO-Revision-Date: 2025-01-11 17:28:21+0000\n"
"Last-Translator: \n"
"Language-Team: \n"
"MIME-Version: 1.0\n"
"Content-Type: text/plain; charset=utf-8\n"
"Content-Transfer-Encoding: 8bit\n"
"X-Generator: GetText.NET Extractor\n"

#: ../../RecipesBrowser.cs:127
msgid "[i:1134]蜂蜜 "
msgstr ""

#: ../../RecipesBrowser.cs:131
msgid "[i:126]水 "
msgstr ""

#: ../../RecipesBrowser.cs:149
msgid "[i:321]墓地 "
msgstr ""

#: ../../RecipesBrowser.cs:123
msgid "[i:3258]徒手 "
msgstr ""

#: ../../RecipesBrowser.cs:135
msgid "[i:4825]岩浆 "
msgstr ""

#: ../../RecipesBrowser.cs:144
msgid "[i:4956]天顶种子 "
msgstr ""

#: ../../RecipesBrowser.cs:140
msgid "[i:593]雪原群系 "
msgstr ""

#: ../../RecipesBrowser.cs:96
#, csharp-format
msgid "{0}配方{1}:"
msgstr ""

#: ../../RecipesBrowser.cs:157
msgid ""
"可合成的物品:\n"
msgstr ""

#: ../../RecipesBrowser.cs:116
msgid "合成站："
msgstr ""

#: ../../RecipesBrowser.cs:61
msgid "未找到物品"
msgstr ""

#: ../../RecipesBrowser.cs:109
msgid "材料："
msgstr ""

#: ../../RecipesBrowser.cs:53
msgid "格式错误!正确格式: /find <物品ID|物品名>"
msgstr ""

#: ../../RecipesBrowser.cs:84
msgid "此物品无配方"
msgstr ""

#: ../../RecipesBrowser.cs:81
#, csharp-format
msgid "物品:{0}"
msgstr ""

#: ../../RecipesBrowser.cs:17
msgid "通过指令获取物品合成表"
msgstr ""


```
### Folder: `temp_RecipesBrowser`
#### File: `temp_RecipesBrowser/manifest.json`
```
{
  "README.en-US": {
    "Description": "Crafting table"
  },
  "README.es-ES": {
    "Description": "Mesa de trabajo"
  },
  "README": {
    "Description": "合成表"
  }
}
```
