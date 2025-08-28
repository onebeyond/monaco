using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Monaco.Template.Backend.Application.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Country",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Country", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "File",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UploadedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsTemp = table.Column<bool>(type: "bit", nullable: false),
                    Discriminator = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    DateTaken = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Dimensions_Height = table.Column<int>(type: "int", nullable: true),
                    Dimensions_Width = table.Column<int>(type: "int", nullable: true),
                    Position_Latitude = table.Column<float>(type: "real", nullable: true),
                    Position_Longitude = table.Column<float>(type: "real", nullable: true),
                    ThumbnailId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_File", x => x.Id);
                    table.ForeignKey(
                        name: "FK_File_File_ThumbnailId",
                        column: x => x.ThumbnailId,
                        principalTable: "File",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InboxState",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Received = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceiveCount = table.Column<int>(type: "int", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Consumed = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Delivered = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxState", x => x.Id);
                    table.UniqueConstraint("AK_InboxState_MessageId_ConsumerId", x => new { x.MessageId, x.ConsumerId });
                });

            migrationBuilder.CreateTable(
                name: "OutboxState",
                columns: table => new
                {
                    OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Delivered = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxState", x => x.OutboxId);
                });

            migrationBuilder.CreateTable(
                name: "Company",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    WebSiteUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Address_Street = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address_City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address_County = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address_PostCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Address_CountryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Company", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Company_Country_Address_CountryId",
                        column: x => x.Address_CountryId,
                        principalTable: "Country",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                columns: table => new
                {
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnqueueTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InboxMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InboxConsumerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InitiatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DestinationAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ResponseAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FaultAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.SequenceNumber);
                    table.ForeignKey(
                        name: "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId",
                        columns: x => new { x.InboxMessageId, x.InboxConsumerId },
                        principalTable: "InboxState",
                        principalColumns: new[] { "MessageId", "ConsumerId" });
                    table.ForeignKey(
                        name: "FK_OutboxMessage_OutboxState_OutboxId",
                        column: x => x.OutboxId,
                        principalTable: "OutboxState",
                        principalColumn: "OutboxId");
                });

            migrationBuilder.CreateTable(
                name: "Product",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefaultPictureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Product", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Product_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Product_File_DefaultPictureId",
                        column: x => x.DefaultPictureId,
                        principalTable: "File",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductPicture",
                columns: table => new
                {
                    PicturesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPicture", x => new { x.PicturesId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_ProductPicture_File_PicturesId",
                        column: x => x.PicturesId,
                        principalTable: "File",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductPicture_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "Country",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { new Guid("003548f7-318a-6f99-8a27-d62963b64cb5"), "China" },
                    { new Guid("01deb5a8-6bd7-1879-9d4b-cb73dd0e9a97"), "Cameroon" },
                    { new Guid("01ec70b8-6aad-3770-a129-62c0fc294791"), "Argentina" },
                    { new Guid("038fb0db-7012-50fb-9ace-3b9f240d9627"), "Jamaica" },
                    { new Guid("03a82f52-22d5-8259-1072-429791258b72"), "South Africa" },
                    { new Guid("08b5d3d0-89ed-a66f-3175-05316ae4a309"), "Bahamas" },
                    { new Guid("09ee26e3-5c82-221e-0729-e47d663a949c"), "Morocco" },
                    { new Guid("0d69d315-2825-2d05-9c04-ba1ad47180ac"), "Bangladesh" },
                    { new Guid("10ef2f04-4d96-5b5e-316f-b373a88731be"), "North Korea" },
                    { new Guid("12d9457c-1c12-9961-60cf-ec2ee534843a"), "Japan" },
                    { new Guid("16eded64-8619-2b73-682f-a703c1cb2d76"), "Costa Rica" },
                    { new Guid("16ff3157-a061-2e81-5ca6-b71e3619376e"), "Papua New Guinea" },
                    { new Guid("18bd53bd-3519-69ef-1148-3453ad744c0f"), "Ireland" },
                    { new Guid("1995e756-0e82-9463-1627-feeb766d2d0d"), "Serbia" },
                    { new Guid("19be2963-072b-2f4b-0f85-558d90ee1770"), "Philippines" },
                    { new Guid("1a426c53-787f-527c-5471-c505d6403603"), "Bolivia" },
                    { new Guid("1b837dfa-0bda-54f3-918a-beef19f691e5"), "Ukraine" },
                    { new Guid("1b84f08e-8e6d-5779-967c-a162c2307153"), "Paraguay" },
                    { new Guid("1e2e1979-539d-128f-538e-c30b8ba49b42"), "Latvia" },
                    { new Guid("1e3241f2-78b6-56e8-4c2d-4a50647973cf"), "Malta" },
                    { new Guid("1e9ab108-4d4b-1812-21d9-5c90d7d897ea"), "Saudi Arabia" },
                    { new Guid("1ff73b0c-27c4-9d4a-3d5b-ec77a6612d56"), "Seychelles" },
                    { new Guid("2092625a-45e4-166e-53cf-bb48408f1b09"), "Liberia" },
                    { new Guid("20bfffe5-7601-63e6-3574-9346e80d5d5c"), "Kyrgyzstan" },
                    { new Guid("26253117-7664-92a9-1a3c-b15cf4bf4c78"), "Andorra" },
                    { new Guid("269928e1-37c5-32e0-60c5-bc6da2b8240e"), "Australia" },
                    { new Guid("28dc2817-94b4-955d-10bc-6a8793dd386c"), "Vanuatu" },
                    { new Guid("2908f020-3d75-3f9a-1a7f-020d1f8d5553"), "Ecuador" },
                    { new Guid("29e7cce5-28a0-990c-6ded-54e96cad9caa"), "Lebanon" },
                    { new Guid("2b1486b9-0cfa-2dc6-3f70-4ee2f11955b4"), "Portugal" },
                    { new Guid("2c4ab1fa-60da-7c87-35b3-ac6903455853"), "Nepal" },
                    { new Guid("2ca00749-35c0-511b-9c2c-b53e5a8f0a71"), "Nigeria" },
                    { new Guid("2cfae83c-0f45-72d1-4624-5af1e10e6147"), "Uganda" },
                    { new Guid("2f02c930-1d71-8ca6-49e7-0d3679a522ea"), "Uzbekistan" },
                    { new Guid("2f7432c5-405f-3f99-4bc3-35e318bd66cc"), "Mexico" },
                    { new Guid("2f7c276f-4d0d-9368-4132-c04149924bb5"), "Trinidad and Tobago" },
                    { new Guid("341651be-9d9c-206b-9162-aad732979787"), "Malawi" },
                    { new Guid("350dc86e-03b2-2c20-1908-b4de7d179de7"), "Nicaragua" },
                    { new Guid("359da8da-68eb-9466-855d-f0f381f715b8"), "Antigua and Barbuda" },
                    { new Guid("362ac8d8-6fba-a641-8272-958f9e553b54"), "Rwanda" },
                    { new Guid("37425b6c-2940-370c-805d-27cb7af97f88"), "Samoa" },
                    { new Guid("3881b3b6-a332-3d35-8a73-43acfbf9045b"), "Palau" },
                    { new Guid("3cb2d6c7-8bb5-191d-183d-b201063d5491"), "Senegal" },
                    { new Guid("40563307-5fe1-97d4-9d10-d81af5138548"), "Malaysia" },
                    { new Guid("408f1ee6-42d3-a211-67c5-32f601b79bd9"), "Madagascar" },
                    { new Guid("420b0acb-a1b0-27e5-45fc-3c6e96f25361"), "Saint Vincent and the Grenadines" },
                    { new Guid("42f62587-3470-5967-77e3-f9b0cf285d90"), "Cambodia" },
                    { new Guid("439e3108-0908-4d90-6f5c-1974362b74b1"), "Thailand" },
                    { new Guid("43da6311-4ed9-9a74-9a46-82847f79a7c3"), "Maldives" },
                    { new Guid("4676b79b-461c-95de-9871-041d2e0b130b"), "Cabo Verde" },
                    { new Guid("46fd6123-5206-2c2c-2832-953d13847069"), "Montenegro" },
                    { new Guid("472e0e57-77f5-9c34-8e10-a621c97108f7"), "New Zealand" },
                    { new Guid("47ac0edf-9beb-345b-0e4b-bccb0c1a2032"), "Qatar" },
                    { new Guid("47e7cc9c-4368-8ab3-056b-66a1351c24cd"), "Timor-Leste" },
                    { new Guid("492a6eb7-5ca3-8e50-3559-c71205b71c3b"), "Togo" },
                    { new Guid("4cf81cef-66a5-a117-3297-884134e903ee"), "Azerbaijan" },
                    { new Guid("4ebfeb1d-67d6-2499-5346-740f737100ee"), "Myanmar (formerly Burma)" },
                    { new Guid("4fa88e4e-344a-32f8-a5ef-4ef2c33858d2"), "Ghana" },
                    { new Guid("5198e2c0-8107-5bcf-90b4-0054eac52295"), "Estonia" },
                    { new Guid("51c38ab5-0b2b-2994-4eb2-c8e82fe17950"), "North Macedonia" },
                    { new Guid("52b13dd7-28fc-9bba-8d59-5c2209531bf5"), "Belarus" },
                    { new Guid("52f81e1e-6903-88e8-199f-cfc5139b0c69"), "Barbados" },
                    { new Guid("534a826b-70ef-2128-1a4c-52e23b7d5447"), "Spain" },
                    { new Guid("53e7d0fc-816f-59dd-7b4a-0ade62330830"), "Slovakia" },
                    { new Guid("555a4e83-0151-7a10-2072-6cc9f4ac77c1"), "Dominican Republic" },
                    { new Guid("55cafe8e-1585-278e-5736-bab16f1b1b8d"), "South Korea" },
                    { new Guid("572563f8-2f44-603d-857e-3f8230035e82"), "Mauritania" },
                    { new Guid("57dc70e9-17ec-995b-5a60-a7c029b1476a"), "Denmark" },
                    { new Guid("59bee04d-32a1-1e53-4a1b-599671a4a693"), "Iraq" },
                    { new Guid("5a3893d1-1e36-310c-7633-8f36ffa26315"), "Vietnam" },
                    { new Guid("5b9d725f-4e89-0181-74ae-ba87e96590d7"), "Indonesia" },
                    { new Guid("5c064eff-a037-a6a3-06ec-92f662903af3"), "Afghanistan" },
                    { new Guid("5e7808be-01a7-9495-2320-314ce20871f3"), "Eritrea" },
                    { new Guid("5ee9fcd4-5680-3cb4-1174-d85201e82367"), "Luxembourg" },
                    { new Guid("5f47ed3b-0b54-95aa-93e8-16bf035c9247"), "Solomon Islands" },
                    { new Guid("60a26e14-90ee-5838-4d27-3672a8e88c48"), "Haiti" },
                    { new Guid("6133196d-26d0-5f8e-5792-6215aefd668d"), "Pakistan" },
                    { new Guid("637e0fb3-34ed-6a1b-7524-3d6cd3155d35"), "Ethiopia" },
                    { new Guid("63a948ca-178b-93da-4fdd-e6be10b49a42"), "Colombia" },
                    { new Guid("6459ce24-8398-57e0-4a31-48addb374e6d"), "Fiji" },
                    { new Guid("6704c135-5af7-88e9-16a5-ad4656fa1be2"), "Israel" },
                    { new Guid("6a265dd9-314e-2eb2-a4f8-3faf9c11a39d"), "Monaco" },
                    { new Guid("6a5e5c1a-4d04-237e-8415-a7f93945133b"), "Laos" },
                    { new Guid("6c4a7b61-3bfd-4a47-95a3-0ca86c72521f"), "Sudan" },
                    { new Guid("6d2bc2ca-2f37-3698-7f08-b7b3eeda5bf5"), "Comoros" },
                    { new Guid("6df0d56f-3b4e-33fe-6d5f-74da585ea5d0"), "Sierra Leone" },
                    { new Guid("6dfb0c67-2ab9-a5bc-1c58-5eaf76016b9d"), "Grenada" },
                    { new Guid("6fa56ae5-2091-39c4-0010-ae74ffa2a0f2"), "Russia" },
                    { new Guid("72499e6c-80a1-32c3-2692-7216abe7815b"), "Brazil" },
                    { new Guid("758d4f58-7371-36c1-26c2-fcdb993806eb"), "Chad" },
                    { new Guid("75c8834a-67e9-9ee5-65bb-e2db6a937074"), "Sao Tome and Principe" },
                    { new Guid("7708a52a-1d62-8f8b-0a4d-6028ed834994"), "Kazakhstan" },
                    { new Guid("77b88575-0e8c-82d0-72bf-06f638b7485f"), "Central African Republic" },
                    { new Guid("7915a976-5224-94f8-232f-ac163a11630c"), "Croatia" },
                    { new Guid("7b9c857e-3fbd-3d19-38cf-f204da39890c"), "Syria" },
                    { new Guid("7d62846b-1841-882f-1cc4-85e2edeea265"), "Armenia" },
                    { new Guid("7fce71e6-a14d-5d96-2dc1-1fc6779e02d3"), "Austria" },
                    { new Guid("81f4a56e-5d53-47f3-52c3-2cd843434126"), "Hungary" },
                    { new Guid("859adf7d-1bf9-87d1-6a9b-4a06ebab6796"), "Oman" },
                    { new Guid("896ef05e-0fe4-92a6-229e-63d7a26e0625"), "Sweden" },
                    { new Guid("89bd993a-4eaa-6bae-8cf9-22f766fa2a1e"), "Egypt" },
                    { new Guid("8b9ac6d0-2320-3f99-8db7-d78c5d53151a"), "Belgium" },
                    { new Guid("8c6486fe-2680-4725-1ef3-3d56e3100f08"), "Djibouti" },
                    { new Guid("8f9ec4fb-916f-90ea-5162-f486a0fc0893"), "United Kingdom" },
                    { new Guid("912f9045-7a3b-151b-2c06-19f899d1787a"), "Singapore" },
                    { new Guid("914f8571-1cac-3f1b-14ef-71aa91836616"), "Georgia" },
                    { new Guid("932a43ac-56dc-951e-7de5-996314e92e9c"), "Sri Lanka" },
                    { new Guid("93ba288b-2a62-1880-1e20-aeb705431890"), "Tunisia" },
                    { new Guid("94e831a3-3e52-7bb4-7616-dc0ac75c2d1d"), "Micronesia" },
                    { new Guid("95938676-73d1-2031-219d-dc67ba314bdf"), "Tuvalu" },
                    { new Guid("967f775b-3c8b-889d-5189-ee71f59f520e"), "Gambia" },
                    { new Guid("97c787c7-915b-4878-3aa2-6ca1eec02f65"), "Chile" },
                    { new Guid("99532f86-827d-39ba-1c01-b9795ec60bf4"), "Bulgaria" },
                    { new Guid("9a0a2c1d-3475-3554-9d18-b11946af6086"), "Suriname" },
                    { new Guid("9a79f7fb-27a4-811b-88c3-9de906521017"), "Palestine State" },
                    { new Guid("9b36a6c2-4739-69a2-068b-c0b3c87c6f67"), "Niger" },
                    { new Guid("9c239e38-640c-3fb2-0bc8-8edaace24c70"), "Iceland" },
                    { new Guid("9c9e3bca-5880-437f-4f18-d6998d90173f"), "Venezuela" },
                    { new Guid("9d2d676f-97fb-952c-06a9-09d4e9696631"), "South Sudan" },
                    { new Guid("9d8cf3fc-222d-a56c-211b-8e8e9edf54e0"), "Libya" },
                    { new Guid("9f420922-9842-0d2d-616d-dacee7c25db7"), "Algeria" },
                    { new Guid("a0d1f65e-307d-59fa-7792-24a5f0359889"), "India" },
                    { new Guid("a2689ae3-3643-6250-a748-8f055cc72da8"), "Yemen" },
                    { new Guid("a2c0a247-350e-85e6-9009-8106f5fd49b2"), "Holy See" },
                    { new Guid("a484eabd-775b-4b7d-595b-3f5d857f5052"), "Mongolia" },
                    { new Guid("a522756f-2fd9-48d1-7443-dd1546fd8b37"), "Slovenia" },
                    { new Guid("a5d734a1-021d-9b50-6fd4-b8f24bb96b12"), "France" },
                    { new Guid("a64406fb-51f7-2ed2-31af-847f9cf16783"), "Gabon" },
                    { new Guid("a681eaeb-1fa0-12d7-93fb-e33a13c188e9"), "Lithuania" },
                    { new Guid("a8b54a61-35d8-9b82-76e4-c709561d9952"), "Albania" },
                    { new Guid("a9c5d1ec-319a-085c-1ee3-80ae15bd27ed"), "Turkmenistan" },
                    { new Guid("a9fe165c-96e6-6bdf-99b0-68f7b2972ef6"), "Greece" },
                    { new Guid("aa29ea0b-2681-3475-6e16-1b7d164b081a"), "Namibia" },
                    { new Guid("aa6f7328-8f52-9bd1-0bba-9fb4f18a926e"), "Mozambique" },
                    { new Guid("aa9d471d-3f2f-5d42-3d01-443a83c12f59"), "Jordan" },
                    { new Guid("ab1e3408-7157-2629-29a5-fbd6a61d92a9"), "El Salvador" },
                    { new Guid("abe01fc7-a484-7a8e-74b6-c5fa27554505"), "Moldova" },
                    { new Guid("afa8b53f-45e6-391f-1c60-db81be664b1b"), "Iran" },
                    { new Guid("b0ac51a0-0128-4070-a49f-19b3dc5e8135"), "Guinea" },
                    { new Guid("b232a961-1224-244f-27a1-06cba2046c22"), "Liechtenstein" },
                    { new Guid("b58da294-9556-8ece-163f-3d89514017a7"), "Somalia" },
                    { new Guid("b6667319-a515-1cf5-7392-92e15c52438d"), "San Marino" },
                    { new Guid("b6ed03e6-096c-3f87-093d-f3ff4bf34c24"), "Cuba" },
                    { new Guid("ba408210-2ea9-8c6c-1d5d-7e33973f1c99"), "Brunei" },
                    { new Guid("ba510e09-5109-42a2-63f0-32d7ede12a5a"), "Angola" },
                    { new Guid("bb0b41a9-7363-5922-9ce0-939412a9036e"), "Switzerland" },
                    { new Guid("bdf3f266-9570-2bb9-330f-9ea51c927068"), "Marshall Islands" },
                    { new Guid("be447a08-0a85-5779-8c65-cf15c2c9a5a8"), "Zambia" },
                    { new Guid("c12db9c7-7ca1-4aec-34f1-7d0bb4713ca8"), "Belize" },
                    { new Guid("c1c8ec1f-58ce-3931-7ad7-ad4b55c14a85"), "Norway" },
                    { new Guid("c3553d72-9d17-04d8-6692-12a5a7250008"), "Saint Kitts and Nevis" },
                    { new Guid("c3700331-07c2-264c-2335-ba61bc718cac"), "Mali" },
                    { new Guid("c576d8f0-5300-2436-2d17-48b699214549"), "Turkey" },
                    { new Guid("c776f397-182b-6d0d-09f2-4e440dc093d3"), "Zimbabwe" },
                    { new Guid("c79ec6ec-2f3d-0c12-8658-5ccbc1a20a77"), "Guyana" },
                    { new Guid("c7fc5498-271d-1027-343a-1560793d1c26"), "Kiribati" },
                    { new Guid("c8f8a591-79c3-a19e-94dd-1c96ed1aa23a"), "Guatemala" },
                    { new Guid("ca111c84-983b-4525-054c-d14dee3a422c"), "United States of America" },
                    { new Guid("caf14a3d-777d-0ee5-63a9-1c3da4e81458"), "Czechia (Czech Republic)" },
                    { new Guid("cb245d05-3293-7315-7ee1-eef8a383319c"), "Netherlands" },
                    { new Guid("cf1fcfbe-11fa-7009-0d71-d2cf5c80a334"), "Italy" },
                    { new Guid("cf59ee2f-144d-99ac-9048-eafaba1f4732"), "Bhutan" },
                    { new Guid("cfa8f583-23d4-1475-57ea-ef72ae7b633f"), "Eswatini (fmr. \"Swaziland\")" },
                    { new Guid("d2300477-7258-17b8-7e8a-cf65f06a325f"), "Honduras" },
                    { new Guid("d4249520-8674-a2c1-0084-683d7aea64db"), "Germany" },
                    { new Guid("d5cb2660-9af9-0385-8a99-52871814043d"), "Romania" },
                    { new Guid("d726f074-4b1d-3043-39eb-f9b4ab9b2344"), "Finland" },
                    { new Guid("d7506bd0-454d-1b83-39e0-905836535271"), "Cyprus" },
                    { new Guid("d7bd1f34-6ef7-43a6-24ed-53bca003086c"), "Bahrain" },
                    { new Guid("d7cd92d3-4522-5a78-3533-816fc61a293f"), "Tajikistan" },
                    { new Guid("d7e310ab-99c3-0a50-2ead-57a2823433ee"), "Dominica" },
                    { new Guid("d9060b41-6ea0-8efa-1dfe-ce293774947b"), "Guinea-Bissau" },
                    { new Guid("d9a21a29-a0e2-71bc-7308-548cc27b19f1"), "Nauru" },
                    { new Guid("d9e7fd6d-77f2-8e76-0174-4c17df936d4e"), "Congo (Congo-Brazzaville)" },
                    { new Guid("da82e26d-40bd-0866-0822-3d5468a36c3e"), "Democratic Republic of the Congo" },
                    { new Guid("dbe19dde-374b-68a8-41e0-48d5195aa6f5"), "Lesotho" },
                    { new Guid("e0276db5-123f-6028-9e3e-eb43f2e06c47"), "Botswana" },
                    { new Guid("e34dc09e-2df2-3661-90e1-f03154d45caf"), "Benin" },
                    { new Guid("e376c876-6960-270c-8744-583fb7a72f55"), "Tonga" },
                    { new Guid("e58f3cfd-069a-4457-23ef-9df8d7fe9ba0"), "Equatorial Guinea" },
                    { new Guid("e8b389a9-0afd-4904-46af-464012f102c2"), "Mauritius" },
                    { new Guid("ee007dac-7417-54e4-18c2-ec6a678ba130"), "Kuwait" },
                    { new Guid("ee06c3ba-4e8c-95c3-88da-6be3f23b9aaa"), "Uruguay" },
                    { new Guid("f16e701a-5dbf-a441-3305-20ac536d34dc"), "Burkina Faso" },
                    { new Guid("f27f4ccb-4e04-8e05-456e-621788247647"), "Kenya" },
                    { new Guid("f5b0c42e-3b0e-3808-1922-fdae58ea075c"), "Panama" },
                    { new Guid("f625e01e-4a07-7ff3-0a7e-ce6e27b586ff"), "Peru" },
                    { new Guid("f7675604-4744-6d6f-a077-67a3e0c85324"), "United Arab Emirates" },
                    { new Guid("f8135306-1323-4f48-7a4b-8d0427cb075b"), "Côte d'Ivoire" },
                    { new Guid("fa81562d-1bc4-944c-86a9-6cc5af502265"), "Tanzania" },
                    { new Guid("fae877c5-4806-05e8-2068-950cdcea6ba9"), "Bosnia and Herzegovina" },
                    { new Guid("fc432d5c-66dd-5660-8186-da84b4e164a0"), "Poland" },
                    { new Guid("fd3ab0e3-279a-107a-88e0-93ee1bb45ffb"), "Saint Lucia" },
                    { new Guid("ff0258f4-35a2-220b-9d9d-f0fe28226cc3"), "Burundi" },
                    { new Guid("ff7c5aa2-6b56-218e-491a-42f4c67ca4a9"), "Canada" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Company_Address_CountryId",
                table: "Company",
                column: "Address_CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_File_ThumbnailId",
                table: "File",
                column: "ThumbnailId",
                unique: true,
                filter: "[ThumbnailId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InboxState_Delivered",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                table: "OutboxMessage",
                columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
                unique: true,
                filter: "[InboxMessageId] IS NOT NULL AND [InboxConsumerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                table: "OutboxMessage",
                columns: new[] { "OutboxId", "SequenceNumber" },
                unique: true,
                filter: "[OutboxId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                table: "OutboxState",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_Product_CompanyId",
                table: "Product",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Product_DefaultPictureId",
                table: "Product",
                column: "DefaultPictureId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Product_Title",
                table: "Product",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPicture_PicturesId",
                table: "ProductPicture",
                column: "PicturesId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductPicture_ProductId",
                table: "ProductPicture",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessage");

            migrationBuilder.DropTable(
                name: "ProductPicture");

            migrationBuilder.DropTable(
                name: "InboxState");

            migrationBuilder.DropTable(
                name: "OutboxState");

            migrationBuilder.DropTable(
                name: "Product");

            migrationBuilder.DropTable(
                name: "Company");

            migrationBuilder.DropTable(
                name: "File");

            migrationBuilder.DropTable(
                name: "Country");
        }
    }
}
