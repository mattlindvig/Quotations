﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Interfaces.Controller;
using Microsoft.AspNetCore.Mvc;
using ServiceModel;

namespace Quotations.Api.Controllers
{
    [Route("api/[controller]")]
    public class QuoteController : Controller
    {
        private IQuoteController _ctrl;
        public QuoteController(IQuoteController ctrl)
        {
            _ctrl = ctrl;
        }

        // GET api/values/5
        [HttpGet("")]
        public List<Quote> Get([FromQuery] string searchText)
        {
            List<Quote> quotes;
            if(!string.IsNullOrEmpty(searchText))
            {
                quotes =  _ctrl.GetQuotes(searchText);
            }
            else
            {
                quotes = _ctrl.GetAllQuotes();
            }
            return quotes;
        }

        [HttpGet("{id}")]
        public Quote Get(int id)
        {
            return _ctrl.GetQuote(id);
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody]Quote value)
        {
            _ctrl.Add(value);
        }

        //// PUT api/values/5
        //[HttpPut("{id}")]
        //public void Put(int id, [FromBody]string value)
        //{
        //}

        //// DELETE api/values/5
        //[HttpDelete("{id}")]
        //public void Delete(int id)
        //{
        //}
    }
}
