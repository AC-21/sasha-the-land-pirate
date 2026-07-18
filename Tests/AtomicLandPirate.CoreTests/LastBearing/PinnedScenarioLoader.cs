#nullable enable

using System;
using System.Collections.Generic;

namespace AtomicLandPirate.LastBearingTests
{
    internal sealed class ScenarioPin
    {
        public ScenarioPin(
            string id,
            string definitionSha256,
            string fixtureSha256,
            string startingStateSha256,
            string contentSetSha256,
            string inputScriptSha256,
            string targetBindingsSha256)
        {
            Id = id;
            DefinitionSha256 = definitionSha256;
            FixtureSha256 = fixtureSha256;
            StartingStateSha256 = startingStateSha256;
            ContentSetSha256 = contentSetSha256;
            InputScriptSha256 = inputScriptSha256;
            TargetBindingsSha256 = targetBindingsSha256;
        }

        public string Id { get; }
        public string DefinitionSha256 { get; }
        public string FixtureSha256 { get; }
        public string StartingStateSha256 { get; }
        public string ContentSetSha256 { get; }
        public string InputScriptSha256 { get; }
        public string TargetBindingsSha256 { get; }
    }

    internal static class PinnedScenarioLoader
    {
        private static readonly IReadOnlyDictionary<string, ScenarioPin> Pins =
            new Dictionary<string, ScenarioPin>(StringComparer.Ordinal)
            {
                ["SCN_COMPOSITION_LOOP_SMOKE"] = new ScenarioPin(
                    "SCN_COMPOSITION_LOOP_SMOKE",
                    "b6e704710a66122958ed855517e24456858207a4a5df6f06c5f593b4d0a4d3e0",
                    "99ab3570c70c41294cdf3c30c2c85d1281cbca1557bb530bf37a811b282860b7",
                    "86b8e72f7a446b996368c2e0e899ed9227b6bbf32e8ac560c346eec118d5c41f",
                    "a32dff01d902d71e1b887e93592ebf760f50df1f2288e3f6c34f99ccdefea1ce",
                    "981796be4ec79c0d56e907c9fc6e25b230bdbdbecbd23075550c4d8faa04d639",
                    "6f7da9adf31c4186442a56ba920c8893b9b4a042ecf10ed2a665a6a309b1ffe3"),
                ["SCN_TIME_POLICY"] = new ScenarioPin(
                    "SCN_TIME_POLICY",
                    "713f22fc96413285b39f98665215c3da824fae2be73ff0ca778a3807ffef6e7c",
                    "498222940aca74c92b10a74aef6fa9f76e80a7480637821f837feef7e366d51f",
                    "4f690c7a757de509af23f774946782d897a990c06af8ed78e7ab370efe7dfd54",
                    "e9aebd6e79ceb0d9f69c8e715e9b3a81e5aa339ddda4d04d3ccad135e8963b70",
                    "f9cf620470891163bbcf20625be40c5a654c05ad812d90c882645155c0c161b4",
                    "c435e70f51475d2e6555f88416c0a707278c24b4595ee0c1e2ab96714f5b1861"),
                ["SCN_PREPARATION_MODULE_MATRIX"] = new ScenarioPin(
                    "SCN_PREPARATION_MODULE_MATRIX",
                    "1dd0ced51deb1c3c5b2a808a37262eb47fdd521be83e2255b0c29ced0043253c",
                    "5e271da24f57c4149f1bd9019bbcef846a82a460451d6f53f294f2763517d1ec",
                    "fcd5a6878706a5c6897cc6dd09418e4c7da2a62457159ce3318b7f392738aab9",
                    "585e7d3cba121cb3087c50d38137dc72acc170cfd24523efb63de7b487656680",
                    "0e02366bb0cdc350d797196072c79b0114559e801edf18a2367b694162f465d1",
                    "bc7afd6b62e29052610597fe6cc34a967f07e4f2b796f3239d777e1f64e081d9"),
                ["SCN_FACTION_WAIT_CLAIM"] = new ScenarioPin(
                    "SCN_FACTION_WAIT_CLAIM",
                    "b4faa15defbed165a13fe2cdbb7a3a4badcd459d0e51517f7c1a06194bed74ea",
                    "824228f2b2ef037befa0c27fa46c12db20dbdcdac0a67e0849d39e9ffbea2925",
                    "4a49fcf6cad6bbaf60257c6b511590119bdd83092bf5d13ac9b3351f12eb508d",
                    "e6968e7f89f59ec0927949194557748ee143b3519a8e3aad95a4dedf70bc18f7",
                    "489b2bf6e6fcd5d66e7d15212005394b63214357485cae48d990b279104ef26d",
                    "4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945"),
                ["SCN_BEARING_COOPERATE"] = new ScenarioPin(
                    "SCN_BEARING_COOPERATE",
                    "12aa4bce297fa9766a4c979c4b5a99d11791cd6d0b0326d3e6cdec02bcae248a",
                    "ff3da18bc53b9fc45a4c15b2d539a4fd90d9e4f232028a80fe082f5250524500",
                    "2e7c2cd0575c79abc69ec1ec07072071ad39cf92b6977c348872ae810b5de63d",
                    "b8906031e29ee2433a6882eda55cc0aa2529908fbb4ec90d4975fed9662db26c",
                    "7cc63ab6d5fa76bc098f3e4ae3e9da2c3af22e4481ce9b213dcb06a21f80092a",
                    "be3cf848317d7915a66daaef373c1d68ef4cba0da953d180789f98b1ead7b77a"),
                ["SCN_BEARING_TAKE"] = new ScenarioPin(
                    "SCN_BEARING_TAKE",
                    "9be20e321a86395bfb32e8224c8a758974fe201f241db92004be659f9b018cef",
                    "35d022dc33c07a7db0296791e91ad5e525b284b0474c09e2dca9f5a5dc56364f",
                    "999f39658b1e29525073002831879ad840e9eb3d98e86a92d1d884ffd02d1233",
                    "d697ea07d5e6acda89f8c6f681f9536bd4503bb24f4cc1b82d1682fd3495f19c",
                    "c929806461b948d33426cd57dff51045aac9662cb519fd840d78ad89f7237b11",
                    "4004f4975af68d1d6fadfcce6fd8cbda9a0296a9e1e97f7da1b9bfd1d3598b72"),
            };

        public static ScenarioPin Get(string id)
        {
            if (!Pins.TryGetValue(id, out ScenarioPin? pin))
            {
                throw new InvalidOperationException("unknown protected scenario: " + id);
            }

            return pin;
        }
    }
}
