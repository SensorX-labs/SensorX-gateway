using System.Security.Cryptography;

// ──────────────────────────────────────────────────────────
//  SensorX Key Generator
//  Generates RSA-2048 key pair for JWT signing (RS256)
// ──────────────────────────────────────────────────────────

var outputDir = args.Length > 0 ? args[0] : ".";
Directory.CreateDirectory(outputDir);

using var rsa = RSA.Create(2048);

var privateKeyPem = rsa.ExportRSAPrivateKeyPem();
var publicKeyPem  = rsa.ExportRSAPublicKeyPem();

var kid = $"key-{DateTime.UtcNow:yyyy-MM}";

var privatePath = Path.Combine(outputDir, "private.key");
var publicPath  = Path.Combine(outputDir, "public.key");

File.WriteAllText(privatePath, privateKeyPem);
File.WriteAllText(publicPath, publicKeyPem);

Console.WriteLine("═══════════════════════════════════════════════");
Console.WriteLine("  SensorX RSA-2048 Key Pair Generated");
Console.WriteLine("═══════════════════════════════════════════════");
Console.WriteLine($"  KID:         {kid}");
Console.WriteLine($"  Private Key: {Path.GetFullPath(privatePath)}");
Console.WriteLine($"  Public Key:  {Path.GetFullPath(publicPath)}");
Console.WriteLine("═══════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine("  IMPORTANT:");
Console.WriteLine("  • Private key → Gateway only (Docker Secret)");
Console.WriteLine("  • Public key  → Internal services (JWKS or file)");
Console.WriteLine("  • Never commit private.key to source control!");
Console.WriteLine();

// Output SQL INSERT for signing_keys table
Console.WriteLine("  SQL to register in signing_keys table:");
Console.WriteLine($"  INSERT INTO signing_keys (kid, algorithm, public_key, is_active)");
Console.WriteLine($"  VALUES ('{kid}', 'RS256', '{publicKeyPem.ReplaceLineEndings("\\n")}', true);");
Console.WriteLine();
