#!/usr/bin/env ruby

require 'prime'

puts Prime.take_while { |prime| prime <= 1_000_000 } * ', '
