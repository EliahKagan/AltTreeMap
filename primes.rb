#!/usr/bin/env ruby

require 'prime'

puts Prime.take_while { |prime| prime <= 10_000_000 } * ', '
