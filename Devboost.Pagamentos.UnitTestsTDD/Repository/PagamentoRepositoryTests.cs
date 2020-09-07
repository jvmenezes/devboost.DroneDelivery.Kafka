using AutoBogus;
using Devboost.Pagamentos.Domain.Entities;
using Devboost.Pagamentos.Repository.Implementation;
using Devboost.Pagamentos.Repository.Model;
using KellermanSoftware.CompareNetObjects;
using ServiceStack;
using ServiceStack.OrmLite;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;
using Devboost.Pagamentos.IoC;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Devboost.Pagamentos.Domain.Enums;

namespace Devboost.Pagamentos.UnitTestsTDD
{
	public class PagamentoRepositoryTests
	{
		private readonly IAutoFaker _faker;
		private readonly CompareLogic _comparison;
	

		public PagamentoRepositoryTests()
		{
			_faker = AutoFaker.Create();
			_comparison = new CompareLogic();
            var serviceCollection = new ServiceCollection();
			serviceCollection.ResolveConverters().BuildServiceProvider();
        }

		[Fact(DisplayName = "Inserir")]
		[Trait("PagamentoRepositoryTests", "Repository Tests")]
		public async Task Inserir_Test()
		{
			using var dbconnection = await new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider).OpenAsync();
		
			var baseRepositoryMock = new PagamentoRepository(dbconnection);



			var guid = Guid.NewGuid();
			var guidCartao = Guid.NewGuid();
			var datetime = DateTime.Now;
			var Cartao = new AutoFaker<Cartao>()
				.RuleFor(fake => fake.Id, fake => guidCartao)
				.RuleFor(fake => fake.Bandeira, fake => PagamentoBandeiraEnum.MasterCard)
				.RuleFor(fake => fake.DataValidade, fake => datetime)
				.RuleFor(fake => fake.Tipo, fake => TipoCartaoEnum.Credito)
				.Generate();

			var formaPagamento = new AutoFaker<FormaPagamento>()
                .RuleFor(fake => fake.CartaoID, fake => guidCartao)
				.RuleFor(fake => fake.Cartao, fake => Cartao)
				.Generate();

			var expectresult = new AutoFaker<Pagamento>()
                .RuleFor(fake => fake.FormaPagamento, fake => formaPagamento)
				.RuleFor(fake => fake.Valor, fake => 1)
				.Generate();

			var param = expectresult.ConvertTo<PagamentoEntity>();
			//When
			await baseRepositoryMock.AddUsingRef(param);

			var pagamento = dbconnection.Select<Pagamento>();
			var formaPagamento2 = dbconnection.Select<FormaPagamento>();
			var cartao = dbconnection.Select<Cartao>();
			
			var result = dbconnection.From<Pagamento>()
				.Join<FormaPagamento>()
				.Join<FormaPagamento, Cartao>((fp, ct) => fp.CartaoID == ct.Id)
				.Where(c => c.Id == expectresult.Id);

			var pagamentos = dbconnection.Single(result);

			var comparacao = _comparison.Compare(pagamentos, expectresult);

			Assert.True(comparacao.AreEqual);
			//Then
		}

		[Fact(DisplayName = "RetonarPagamento")]
		[Trait("PagamentoRepositoryTests", "Repository Tests")]
		public async Task RetonarPagamento_Test()
		{
			using var dbconnection = await new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider).OpenAsync();
			//var dbconnection = await new OrmLiteConnectionFactory(conexao,
					//SqlServerDialect.Provider).OpenAsync();
			var baseRepositoryMock = new PagamentoRepository(dbconnection);

			dbconnection.CreateTableIfNotExists<Cartao>();
			dbconnection.CreateTableIfNotExists<FormaPagamento>();
			dbconnection.CreateTableIfNotExists<Pagamento>();

			var guid = Guid.NewGuid();
			var guidCartao = Guid.NewGuid();
			var datetime = DateTime.Now;
			var Cartao = new AutoFaker<Cartao>()
				.RuleFor(fake => fake.Id, fake => guidCartao)
				.RuleFor(fake => fake.Bandeira, fake => PagamentoBandeiraEnum.MasterCard)
				.RuleFor(fake => fake.DataValidade, fake => datetime)
				.RuleFor(fake => fake.Tipo, fake => TipoCartaoEnum.Credito)
				.Generate();

			var formaPagamento = new AutoFaker<FormaPagamento>()
				.RuleFor(fake => fake.Id, fake => guid)
				.RuleFor(fake => fake.CartaoID, fake => guidCartao)
				.RuleFor(fake => fake.Cartao, fake => Cartao)
				.Generate();

			var expectresult = new AutoFaker<Pagamento>()
				.RuleFor(fake => fake.FormaPagamentoID, fake => guid)
				.RuleFor(fake => fake.FormaPagamento, fake => formaPagamento)
				.RuleFor(fake => fake.Valor, fake => 1)
				.RuleFor(fake => fake.StatusPagamento, fake => StatusPagamentoEnum.Aprovado)
				.Generate();

			await baseRepositoryMock.AddUsingRef(expectresult.ConvertTo<PagamentoEntity>());
			Guid idPagamento = expectresult.Id;

			var pgamento = dbconnection.Select<Pagamento>();
			var cartao = dbconnection.Select<Cartao>();
			var formapagamento = dbconnection.Select<FormaPagamento>();

			var result = await baseRepositoryMock.RetonarPagamento(idPagamento);
			result.FormaPagamento.Cartao.DataValidade = datetime;

			var comparacao = _comparison.Compare(result.ConvertTo<Pagamento>(), expectresult);
			var diferenca = comparacao.Differences;
			Assert.True(comparacao.AreEqual);
		}

		[Fact(DisplayName = "Atualizar")]
		[Trait("PagamentoRepositoryTests", "Repository Tests")]
		public async Task Atualizar_test()
		{
			//Given(Prepara��o)
			using var dbconnection = await new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider).OpenAsync();
			var baseRepositoryMock = new PagamentoRepository(dbconnection);

			dbconnection.CreateTableIfNotExists<Pagamento>();
			var expectresult = new AutoFaker<Pagamento>()
				.RuleFor(fake => fake.Valor, fake => 1)
				.Generate();
			
			await dbconnection.InsertAsync(expectresult);
			Pagamento compare = dbconnection.Select<Pagamento>().FirstOrDefault();
			
			expectresult.Valor = 2; 

			var param = expectresult.ConvertTo<PagamentoEntity>();

			//When
			await baseRepositoryMock.Atualizar(param);
			var result = await dbconnection.SingleAsync<Pagamento>(p => p.Id == expectresult.Id);

			//Then
			Assert.True(result.Valor != compare.Valor);
		}

		private Pagamento AtribuirClasse(dynamic pagamentos)
		{
			List<Dictionary<string, string>> propriedades = RetornarPropriedadesDinamicas(pagamentos);
			Pagamento retorno = new Pagamento();
			retorno.Id = AtribuirValor<Guid>(propriedades, "idPagamento");
			retorno.FormaPagamentoID = AtribuirValor<Guid>(propriedades, "FormaPagamentoID");
			retorno.IdPedido = AtribuirValor<Guid>(propriedades, "IdPedido");
			retorno.Valor = AtribuirValor<float>(propriedades, "Valor");

			retorno.FormaPagamento = new FormaPagamento();
			retorno.FormaPagamento.Id = AtribuirValor<Guid>(propriedades, "IdFormaPagamento");
			retorno.FormaPagamento.CartaoID = AtribuirValor<Guid>(propriedades, "idCartao");

			retorno.FormaPagamento.Cartao = new Cartao();
			retorno.FormaPagamento.Cartao.Id = AtribuirValor<Guid>(propriedades, "idCartao");
			retorno.FormaPagamento.Cartao.Bandeira = AtribuirValor<PagamentoBandeiraEnum>(propriedades, "Bandeira");
			DateTime data = AtribuirValor<DateTime>(propriedades, "DataValidade");
			retorno.FormaPagamento.Cartao.DataValidade = new DateTime(data.Year, data.Day, data.Month, data.Hour, data.Minute, data.Second);
			retorno.FormaPagamento.Cartao.CodSeguranca = AtribuirValor<string>(propriedades, "CodSeguranca");
			retorno.FormaPagamento.Cartao.NumeroCartao = AtribuirValor<string>(propriedades, "NumeroCartao");
			retorno.FormaPagamento.Cartao.Tipo = AtribuirValor<TipoCartaoEnum>(propriedades, "Tipo");
			return retorno;
		}
		private List<Dictionary<string, string>> RetornarPropriedadesDinamicas(dynamic objetoDinamico)
		{
			try
			{
				ExpandoObject attributesAsJObject = objetoDinamico;
				var values = (IDictionary<string, object>)attributesAsJObject;
				List<Dictionary<string, string>> toReturn = new List<Dictionary<string, string>>();
				foreach (var item in values)
				{
					Dictionary<string, string> propriedade = new Dictionary<string, string>();
					propriedade.Add(item.Key, item.Value.ToString());
					toReturn.Add(propriedade);
				}
				return toReturn;
			}
			catch (Exception ex)
			{
				throw new Exception(ex.Message);
			}
		}
		private TipoRetorno AtribuirValor<TipoRetorno>(List<Dictionary<string, string>> listaPropriedade, string nomePropriedade)
		{
			string valor = string.Empty;
			listaPropriedade.FirstOrDefault(c => c.ContainsKey(nomePropriedade)).TryGetValue(nomePropriedade, out valor);
			TipoRetorno retorno = valor.ConvertTo<TipoRetorno>();
			return retorno;
		}
	}
}